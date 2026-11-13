#!/usr/bin/env python3
"""Verify the local production catalog after the retired project database stops."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import hashlib
import json
import socket
import urllib.error
import urllib.request

root = Path(__file__).resolve().parent.parent
base = 'http://127.0.0.1:4174'
catalog = json.loads((root / 'src/data/catalog.json').read_text())
baseline = json.loads((root.parent / 'docs/implementation/inputs/2026-11-06-catalog-before-simplify.json').read_text())
evidence = []

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None

opener = urllib.request.build_opener(NoRedirect)

def request(path, method='GET', origin=base):
    try:
        response = opener.open(urllib.request.Request(origin + path, method=method), timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.status, dict(response.headers), response.read()

def check(label, passed, **details):
    evidence.append({'check': label, 'passed': bool(passed), **details})
    if not passed:
        raise AssertionError(f'{label}: {details}')

def refs(value):
    if isinstance(value, str):
        return {value} if value.startswith('/media/') else set()
    if isinstance(value, dict):
        return set().union(*(refs(item) for item in value.values()))
    if isinstance(value, list):
        return set().union(*(refs(item) for item in value))
    return set()

try:
    with socket.socket() as connection:
        check('Dedicated database port is closed', connection.connect_ex(('127.0.0.1', 55432)) != 0)
    status, _, body = request('/api/public/deployment')
    deployment = json.loads(body)
    check('Local catalog deployment', status == 200 and deployment['releaseId'] == catalog['id'] and deployment['manifest']['source'] == 'local-catalog')
    status, _, body = request(f"/api/public/releases/{catalog['id']}/catalog")
    check('HTTP catalog exactly matches local file', status == 200 and json.loads(body) == catalog)
    for locale in ('zh-CN', 'en'):
        for path in (f'/{locale}', f'/{locale}/aquatic', f'/{locale}/materials', f'/{locale}/aquatic/neon-tetra'):
            status, _, _ = request(path)
            check(f'Catalog page {path}', status == 200, status=status)
    retired = ['/admin', '/admin/login', '/api/entities', '/api/users', '/api/graphql', '/api/admin/releases', '/api/admin/releases/retired', '/api/admin/releases/retired/preview', '/api/admin/releases/retired/activate', '/api/admin/releases/retired/rollback']
    for path in retired:
        for method in ('GET', 'POST', 'PATCH', 'DELETE'):
            status, _, _ = request(path, method)
            check(f'Retired {method} {path}', status in (404, 410), status=status)
    for path in [f"/api/public/releases/{catalog['id']}/templates/retired", f"/api/public/releases/{baseline['id']}/catalog", '/api/public/releases/unknown/catalog']:
        status, _, _ = request(path)
        check(f'Unavailable release resource {path}', status == 404, status=status)
    paths = sorted(refs(baseline) | refs(catalog))
    def head(path):
        status, _, _ = request(path, 'HEAD')
        local = root / 'public' / path.lstrip('/')
        return path, status, local.is_file() and hashlib.sha256(local.read_bytes()).hexdigest() == local.stem
    with ThreadPoolExecutor(max_workers=6) as workers:
        results = list(workers.map(head, paths))
    check('All baseline/current media paths and hashes', all(status == 200 and digest for _, status, digest in results), count=len(results), failures=[item for item in results if item[1] != 200 or not item[2]])
    data = b'<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"><!-- catalog runtime persistent-media probe --></svg>'
    filename = hashlib.sha256(data).hexdigest() + '.svg'
    probe = root / 'public/media/releases' / filename
    existed = probe.exists()
    try:
        probe.write_bytes(data)
        status, headers, body = request('/media/releases/' + filename)
        check('New hashed media works after process startup', status == 200 and body == data and 'immutable' in headers.get('Cache-Control', ''), status=status)
    finally:
        if not existed:
            probe.unlink(missing_ok=True)
    status, _, _ = request('/media/releases/' + '0' * 64 + '.svg')
    check('Missing hash media returns 404', status == 404, status=status)
    path = '/en/aquatic?type=fish&q=neon'
    status, headers, _ = request(path, origin='http://127.0.0.1:4173')
    check('Legacy redirect retains path/query', status == 308 and headers.get('Location') == base + path, status=status, location=headers.get('Location'))
finally:
    output = root / '.runtime/evidence/catalog-runtime-http.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n')

print(json.dumps({'passed': len(evidence), 'evidence': str(output)}, indent=2))
