#!/usr/bin/env python3
"""Manage only detached FishAtlas production processes recorded in .runtime/services.json."""
from pathlib import Path
import json, os, signal, socket, subprocess, sys, time, shutil, urllib.request, urllib.error
root=Path(__file__).resolve().parent.parent
runtime=root/'.runtime'
registry=runtime/'services.json'
node=os.environ.get('FISHATLAS_NODE','/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node')
def read():
 try:return json.loads(registry.read_text())
 except FileNotFoundError:return {}
def alive(pid):
 try:os.kill(pid,0);return True
 except ProcessLookupError:return False
def stop():
 state=read()
 for name,item in state.items():
  pid=item['pid']
  if alive(pid):
   command=subprocess.run(['ps','-p',str(pid),'-o','command='],capture_output=True,text=True).stdout
   if item['identity'] not in command:
    cwd=subprocess.run(['lsof','-a','-p',str(pid),'-d','cwd','-Fn'],capture_output=True,text=True).stdout
    own_next=command.strip().startswith('next-server') and any(f'n{folder}\n' in cwd for folder in [root,root/'.next/standalone'])
    if not own_next:raise RuntimeError(f'Refusing to stop an unrelated process at PID {pid}')
   os.kill(pid,signal.SIGTERM)
 for port in (4174,4173):
  for _ in range(100):
   with socket.socket() as connection:
    if connection.connect_ex(('127.0.0.1',port))!=0:break
   time.sleep(.05)
  else:raise RuntimeError(f'Port {port} did not stop; inspect its owner before restarting')
 registry.unlink(missing_ok=True)
 print('Stopped recorded FishAtlas services.')
def start():
 if not (root/'.next/BUILD_ID').exists():raise RuntimeError('Run pnpm build before starting production')
 state=read()
 if any(alive(item['pid']) for item in state.values()):raise RuntimeError('Recorded service already running; inspect service:status first')
 for port in (4174,4173):
  with socket.socket() as connection:
   if connection.connect_ex(('127.0.0.1',port))==0:raise RuntimeError(f'Port {port} is already in use; refusing to attach an unrelated service')
 runtime.mkdir(exist_ok=True)
 env=dict(os.environ,NODE_ENV='production',HOSTNAME='127.0.0.1',PORT='4174',FISHATLAS_PROJECT_ROOT=str(root))
 standalone=root/'.next/standalone'
 (root/'media').mkdir(exist_ok=True)
 for link,target in [(standalone/'public',root/'public'),(standalone/'media',root/'media'),(standalone/'.next/static',root/'.next/static')]:
  if link.is_symlink():
   if link.resolve()!=target:link.unlink()
  elif link.exists():
   backup=runtime/f'standalone-traced-{link.name}-{time.time_ns()}'
   shutil.move(str(link),str(backup))
  if not link.exists():link.symlink_to(target,target_is_directory=True)
 jobs={'web':([node,str(root/'.next/standalone/server.js')],str(root/'.next/standalone/server.js')),'redirect':([node,str(root/'scripts/redirect-legacy.mjs')],str(root/'scripts/redirect-legacy.mjs'))}
 state={}
 for name,(args,identity) in jobs.items():
  log=open(runtime/f'{name}-production.log','ab',buffering=0)
  process=subprocess.Popen(args,cwd=root,env=env,stdin=subprocess.DEVNULL,stdout=log,stderr=subprocess.STDOUT,start_new_session=True,close_fds=True)
  log.close()
  state[name]={'pid':process.pid,'identity':identity,'log':str(runtime/f'{name}-production.log')}
 registry.write_text(json.dumps(state,indent=2)+'\n')
 os.chmod(registry,0o600)
 for _ in range(100):
  if any(not alive(item['pid']) for item in state.values()):raise RuntimeError('Service exited; inspect .runtime/*-production.log')
  try:
   with urllib.request.urlopen('http://127.0.0.1:4174/api/public/deployment',timeout=1) as response:
    if response.status==200:break
  except (urllib.error.URLError,TimeoutError):time.sleep(.2)
 else:raise RuntimeError('Production did not become ready; inspect logs')
 print(json.dumps({'ready':True,'services':state},indent=2))
mode=sys.argv[1] if len(sys.argv)>1 else 'status'
if mode=='start':start()
elif mode=='stop':stop()
elif mode=='status':print(json.dumps({name:{**item,'running':alive(item['pid'])} for name,item in read().items()},indent=2))
else:raise SystemExit(2)
