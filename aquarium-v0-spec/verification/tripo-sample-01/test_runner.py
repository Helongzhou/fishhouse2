"""Offline regression tests: fake CLI only; never submits or contacts Tripo."""
import importlib.util
import json
from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[2] / 'tools/tripo_sample.py'
spec = importlib.util.spec_from_file_location('runner', SOURCE)
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name).resolve()
        self.patches = [patch.object(r, 'ROOT', root), patch.object(r, 'BASE', root / 'art/sample'),
                        patch.object(r, 'VERIFY', root / 'verify')]
        for p in self.patches:
            p.start()
            self.addCleanup(p.stop)
        r.BASE.mkdir(parents=True)
        r.VERIFY.mkdir()
        (r.BASE / 'manifest.json').write_text('{}')
        self.entries = []
        for ident in ['one', 'two']:
            plan = r.BASE / 'plans' / (ident + '.json')
            r.save(plan, {'valid': True})
            self.entries.append({'id': ident, 'credits': 110, 'argv': ['make', ident, '--json'],
                                 'plan_sha256': r.digest(plan)})
        r.save(r.VERIFY / 'preflight-summary.json', {
            'manifest_sha256': r.digest(r.BASE / 'manifest.json'),
            'total_generation_credits': 220, 'assets': self.entries})
        p = patch.object(r, 'assets', return_value=[({'asset_id': e['id']}, e['argv']) for e in self.entries])
        p.start()
        self.addCleanup(p.stop)
        self.opts = SimpleNamespace(execute_paid=True, confirmed_credit_cap=220)

    def success(self, ident, credits=110):
        directory = r.BASE / 'generated' / ident
        directory.mkdir(parents=True, exist_ok=True)
        model = directory / 'model.glb'
        model.write_bytes(b'real-test-bytes')
        return {'exit_code': 0, 'result': {'task_id': 'task_' + ident, 'status': 'success',
                'credits_consumed': credits, 'output_dir': str(directory), 'model_file': str(model)}}

    def test_unconfirmed_no_cli(self):
        with patch.object(r, 'cli') as cli:
            with self.assertRaises(ValueError):
                r.execute(SimpleNamespace(execute_paid=False, confirmed_credit_cap=220))
            cli.assert_not_called()

    def test_unknown_accounting_stops_and_preserves_task(self):
        for amount in [None, float('nan'), float('inf'), -1, True, '110']:
            with self.subTest(amount=amount):
                state = r.VERIFY / 'execution-state.json'
                state.unlink(missing_ok=True)
                response = self.success('one', amount)
                if amount is None:
                    response['result'].pop('credits_consumed')
                with patch.object(r, 'cli', side_effect=[{'exit_code': 0, 'result': {'balance': 1000}}, response]) as cli:
                    with self.assertRaises(ValueError):
                        r.execute(self.opts)
                    self.assertEqual(cli.call_count, 2)
                saved = json.loads(state.read_text())['jobs']['one']
                self.assertEqual(saved['task_id'], 'task_one')
                self.assertEqual(saved['status'], 'needs_attention')
                with patch.object(r, 'cli') as cli:
                    with self.assertRaises(ValueError):
                        r.execute(self.opts)
                    cli.assert_not_called()

    def test_success_repeat_verifies_hashes_without_cli(self):
        balance = {'exit_code': 0, 'result': {'balance': 1000}}
        with patch.object(r, 'cli', side_effect=[balance, self.success('one'), balance, self.success('two')]):
            r.execute(self.opts)
        with patch.object(r, 'cli') as cli:
            r.execute(self.opts)
            cli.assert_not_called()
        state_path = r.VERIFY / 'execution-state.json'
        original = json.loads(state_path.read_text())
        for field in ['credits_consumed', 'sha256', 'model_file']:
            broken = json.loads(json.dumps(original))
            broken['jobs']['one'].pop(field)
            r.save(state_path, broken)
            with patch.object(r, 'cli') as cli:
                with self.assertRaises(ValueError):
                    r.execute(self.opts)
                cli.assert_not_called()
        r.save(state_path, original)
        model = r.BASE / 'generated/one/model.glb'
        model.write_bytes(b'changed')
        with patch.object(r, 'cli') as cli:
            with self.assertRaises(ValueError):
                r.execute(self.opts)
            cli.assert_not_called()
        model.unlink()
        with patch.object(r, 'cli') as cli:
            with self.assertRaises(ValueError):
                r.execute(self.opts)
            cli.assert_not_called()

    def test_failed_submit_never_retried(self):
        with patch.object(r, 'cli', side_effect=[{'exit_code': 0, 'result': {'balance': 1000}},
                {'exit_code': 7, 'result': {'task_id': 'task_uncertain'}}]) as cli:
            with self.assertRaises(ValueError):
                r.execute(self.opts)
            self.assertEqual(cli.call_count, 2)
        with patch.object(r, 'cli') as cli:
            with self.assertRaises(ValueError):
                r.execute(self.opts)
            cli.assert_not_called()

    def test_credit_cap_stops_next_job(self):
        with patch.object(r, 'cli', side_effect=[{'exit_code': 0, 'result': {'balance': 1000}},
                self.success('one', 150), {'exit_code': 0, 'result': {'balance': 1000}}]) as cli:
            with self.assertRaisesRegex(ValueError, 'cap reached'):
                r.execute(self.opts)
            self.assertEqual(cli.call_count, 3)


if __name__ == '__main__':
    unittest.main(verbosity=2)
