#!/usr/bin/env python3
"""Text-only, single-candidate Tripo sample preflight and guarded continuation.
prepare is entirely offline. execute requires --execute-paid and --confirmed-credit-cap.
Never clears an uncertain submission or retries/re-generates an existing job.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'art/tripo-sample-01'
VERIFY = ROOT / 'verification/tripo-sample-01'
MODEL = 'P2-20260801'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + '.tmp')
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    tmp.replace(path)


def cli(args):
    # Constant shell program: prompts and paths are positional argv, never code.
    proc = subprocess.run(['/bin/zsh', '-lc', 'export TRIPO_CLIENT_APP=codex; tripo "$@"',
                           'tripo-sample', *args], capture_output=True, text=True)
    stdout = re.sub(r'tsk_[A-Za-z0-9_-]+', '[REDACTED]', proc.stdout)
    stderr = re.sub(r'tsk_[A-Za-z0-9_-]+', '[REDACTED]', proc.stderr)
    try:
        result = json.loads(stdout)
    except ValueError:
        result = None
    return {'exit_code': proc.returncode, 'result': result, 'stdout': stdout, 'stderr': stderr}


def assets():
    doc = json.loads((BASE / 'manifest.json').read_text())
    rows = doc['assets']
    if len(rows) != 11:
        raise ValueError('Expected exactly 11 approved sample assets')
    seen = set()
    for row in rows:
        ident = row['asset_id']
        if not re.fullmatch(r'[a-z0-9][a-z0-9_-]*', ident) or ident in seen:
            raise ValueError('Invalid or duplicate asset id')
        seen.add(ident)
        prompt = row['prompt_en']
        # The resulting dry-run must prove the CLI inferred text, never image/upload.
        if not isinstance(prompt, str) or not prompt.strip() or len(prompt) > 970:
            raise ValueError('Text prompt must be nonempty and <=970 characters')
        limit = row['tripo_request']['face_limit']
        if not isinstance(limit, int) or not 48 <= limit <= 50000:
            raise ValueError(f'{ident}: missing/invalid P2 face_limit')
        yield row, ['make', prompt,
                    '--model', MODEL, '-n', '1', '-p', 'texture=true', '-p', 'pbr=false',
                    '-p', 'texture_quality=standard', '-p', f'face_limit={limit}',
                    '-o', str(BASE / 'generated' / ident), '--name', 'sample01-' + ident, '--json']


def prepare():
    entries = []
    for row, args in assets():
        record = cli([*args, '--dry-run'])
        save(BASE / 'plans' / (row['asset_id'] + '.json'), record)
        plan = record['result'] or {}
        if record['exit_code'] or not plan.get('valid'):
            raise ValueError(f"Preflight failed: {row['asset_id']}")
        if (len(plan['steps']) != 1 or plan['steps'][0]['name'] != 'text-to-model'
                or plan.get('pending_uploads') or plan.get('candidates') != 1
                or plan['steps'][0]['payload'].get('model') != MODEL
                or plan['steps'][0]['payload'].get('pbr') is not False
                or plan.get('warnings')):
            raise ValueError('Unexpected plan/adjustment: manual review required')
        estimate = plan.get('generation_credits_estimate')
        if not valid_credits(estimate) or estimate <= 0:
            raise ValueError('Unestimated generation cost')
        entries.append({'id': row['asset_id'], 'credits': estimate, 'argv': args,
                        'plan_sha256': digest(BASE / 'plans' / (row['asset_id'] + '.json'))})
    summary = {'manifest_sha256': digest(BASE / 'manifest.json'), 'model': MODEL,
               'assets': entries, 'total_generation_credits': sum(x['credits'] for x in entries),
               'excluded': ['rigging', 'animation/retarget', 'API format conversion', 'rerolls',
                            'extra candidates', 'local Unity/Blender import work'],
               'limitations': 'Offline list-price estimates; not a live quote or server acceptance test.',
               'paid_tasks_submitted': 0}
    save(VERIFY / 'preflight-summary.json', summary)
    print(json.dumps({'prepared': len(entries), 'credits': summary['total_generation_credits']}))


def hashes(result, ident):
    directory = Path(result.get('output_dir', BASE / 'generated')).resolve()
    allowed = (BASE / 'generated' / ident).resolve()
    if not directory.is_dir() or not directory.is_relative_to(allowed):
        raise ValueError('Output directory outside sample generated folder')
    model = Path(result.get('model_file', ''))
    if not model.is_absolute():
        model = ROOT / model
    if not model.is_file() or not model.resolve().is_relative_to(directory):
        raise ValueError('Success response lacks a local sample model file; recover existing task')
    return {str(p.relative_to(ROOT)): digest(p) for p in directory.rglob('*') if p.is_file()}


def valid_credits(value):
    return type(value) in (int, float) and math.isfinite(value) and value >= 0


def verify_success(job):
    if not valid_credits(job.get('credits_consumed')):
        raise ValueError('Unknown billed credits; reconcile existing task before continuing')
    stored = job.get('sha256')
    model = job.get('model_file')
    if not job.get('task_id') or not stored or not model or model not in stored:
        raise ValueError('Missing model/hash evidence; recover existing task')
    allowed = (BASE / 'generated').resolve()
    for name, expected_hash in stored.items():
        path = (ROOT / name).resolve()
        if not path.is_relative_to(allowed) or not path.is_file() or digest(path) != expected_hash:
            raise ValueError('Missing/changed generated artifact; recover existing task')


def execute(opts):
    if not opts.execute_paid or opts.confirmed_credit_cap is None:
        raise ValueError('Requires explicit --execute-paid and --confirmed-credit-cap N')
    summary = json.loads((VERIFY / 'preflight-summary.json').read_text())
    if digest(BASE / 'manifest.json') != summary['manifest_sha256']:
        raise ValueError('Manifest changed: prepare and reconfirm first')
    if opts.confirmed_credit_cap != summary['total_generation_credits']:
        raise ValueError('Confirmed cap must match the reviewed full plan estimate')
    # Exclusive lock prevents concurrent paid execution; do not prepare during execution.
    lock = VERIFY / 'execute.lock'
    fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    state_path = VERIFY / 'execution-state.json'
    try:
        state = json.loads(state_path.read_text()) if state_path.exists() else {'jobs': {}}
        expected = {row['asset_id']: args for row, args in assets()}
        for old_job in state['jobs'].values():
            if old_job.get('status') != 'success':
                raise ValueError('Prior submission needs reconciliation; no resubmission')
            verify_success(old_job)
        for entry in summary['assets']:
            if entry['argv'] != expected[entry['id']]:
                raise ValueError('Execution argv differs from text-only manifest plan')
            if digest(BASE / 'plans' / (entry['id'] + '.json')) != entry['plan_sha256']:
                raise ValueError('Plan changed: prepare and reconfirm first')
            old = state['jobs'].get(entry['id'])
            if old:
                if old.get('status') == 'success':
                    verify_success(old)
                    continue
                raise ValueError(f"{entry['id']} has a prior submission; recover it, never resubmit")
            balance = cli(['balance', '--json'])
            save(VERIFY / 'last-balance.json', balance)
            available = (balance['result'] or {}).get('balance')
            if balance['exit_code'] or not valid_credits(available) or available < entry['credits']:
                raise ValueError('Insufficient/unverified balance: stopped before submission')
            actual = sum(j['credits_consumed'] for j in state['jobs'].values())
            if actual + entry['credits'] > opts.confirmed_credit_cap:
                raise ValueError('Credit cap reached')
            job = {'status': 'submission_uncertain', 'estimated_credits': entry['credits']}
            state['jobs'][entry['id']] = job
            save(state_path, state)  # persists BEFORE any network submission
            record = cli([*entry['argv'], '--yes'])  # blocking CLI owns task watching
            save(VERIFY / (entry['id'] + '-execution.json'), record)
            result = record['result'] or {}
            job.update({'exit_code': record['exit_code'], 'task_id': result.get('task_id'),
                        'credits_consumed': result.get('credits_consumed')})
            job['status'] = 'needs_attention'
            save(state_path, state)  # keep task ID even if artifact/accounting validation fails
            if (record['exit_code'] == 0 and result.get('status') == 'success' and job['task_id']
                    and valid_credits(job['credits_consumed'])):
                artifact_hashes = hashes(result, entry['id'])
                model_path = Path(result['model_file'])
                if not model_path.is_absolute():
                    model_path = ROOT / model_path
                job.update({'status': 'success', 'sha256': artifact_hashes,
                            'model_file': str(model_path.resolve().relative_to(ROOT))})
            save(state_path, state)
            if job['status'] != 'success':
                raise ValueError('Stopped after CLI failure; see saved diagnostics. No retry performed.')
        print('All approved jobs succeeded; see execution-state.json')
    finally:
        os.close(fd)
        lock.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'execute'])
    parser.add_argument('--execute-paid', action='store_true')
    parser.add_argument('--confirmed-credit-cap', type=int)
    opts = parser.parse_args()
    try:
        prepare() if opts.command == 'prepare' else execute(opts)
    except (ValueError, OSError, KeyError) as exc:
        print(str(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
