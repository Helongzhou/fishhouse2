#!/usr/bin/env python3
"""Linux checks for the process-group launcher. Not a Mac run and not a Unity run."""
import os
import signal
import subprocess
import sys
import tempfile
import time

LAUNCHER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "unity6_bounded_launch.py")
sys.path.insert(0, os.path.dirname(LAUNCHER))
import unity6_bounded_launch as launcher

CHECKS = 0
EVIDENCE = os.environ.get("AQUARIUM_LAUNCH_TEST_OUT", "")


def check(condition, name):
    global CHECKS
    if not condition:
        raise SystemExit("FAIL " + name)
    print("PASS " + name)
    CHECKS += 1


def show(name, code, text, extra):
    print("CASE " + name)
    print("returncode %s" % code)
    print(extra)
    print("LOG<<")
    print(text.rstrip())
    print(">>")
    if EVIDENCE:
        handle = open(os.path.join(EVIDENCE, name + ".log"), "w")
        handle.write(text)
        handle.close()


def alive(pid):
    try:
        os.kill(pid, 0)
    except OSError:
        return False
    return True


def run_launcher(timeout, result, log_path, command):
    completed = subprocess.run(
        [sys.executable, LAUNCHER, "--timeout-seconds", str(timeout), "--result", result,
         "--wrapper-log", log_path, "--"] + command,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    text = open(log_path, "r").read() if os.path.isfile(log_path) else ""
    return completed.returncode, text, completed.stderr.decode("utf-8", "replace")


def command_for(script, pidfile):
    return [sys.executable, "-c", script, pidfile]


def main():
    source = open(LAUNCHER, "r").read()
    check("/proc" not in source, "launcher source does not list the process table")
    root = tempfile.mkdtemp(prefix="aquarium-launch-")
    if EVIDENCE:
        os.makedirs(EVIDENCE, exist_ok=True)
    dummy = subprocess.Popen(["sleep", "180"])
    print("dummy_pid %s" % dummy.pid)
    try:
        check(launcher.signal_saved_group(os.getpgrp(), signal.SIGTERM) == "refused", "refuses the parent process group")
        check(alive(dummy.pid), "refusal leaves the dummy alive")
        dead = subprocess.Popen(["sleep", "0.05"], start_new_session=True)
        dead_pgid = os.getpgid(dead.pid)
        dead.wait()
        check(launcher.signal_saved_group(dead_pgid, signal.SIGTERM) == "exited", "saved pgid of an exited group does not raise")
        check(alive(dummy.pid), "signalling an exited group leaves the dummy alive")

        result = os.path.join(root, "ok.json")
        log_path = os.path.join(root, "success.log")
        code, text, _err = run_launcher(5, result, log_path, [sys.executable, "-c", "open(%r,'w').write('ok')" % result])
        show("success", code, text, "result_exists %s dummy_alive %s" % (os.path.isfile(result), alive(dummy.pid)))
        check(code == 0 and "exit 0" in text and "result_present true" in text and "not_run" not in text and "pgid " in text,
              "success keeps exit 0 and does not say not_run")

        log_path = os.path.join(root, "failure.log")
        code, text, _err = run_launcher(5, os.path.join(root, "absent-failure.json"), log_path, [sys.executable, "-c", "raise SystemExit(3)"])
        show("failure", code, text, "dummy_alive %s" % alive(dummy.pid))
        check(code == 3 and "exit 3\n" in text and "result_present false" in text and "stage not_run" in text,
              "failure exit 3 with missing result stays not_run")

        log_path = os.path.join(root, "launch.log")
        code, text, _err = run_launcher(5, os.path.join(root, "absent-launch.json"), log_path, ["/no/such/aquarium-unity"])
        show("launch-failure", code, text, "log_exists %s dummy_alive %s" % (os.path.isfile(log_path), alive(dummy.pid)))
        check(code == 127 and "launch_failed" in text and "pid " not in text, "missing executable fails before a process group exists")

        log_path = os.path.join(root, "empty.log")
        code, text, _err = run_launcher(5, os.path.join(root, "absent-empty.json"), log_path, [])
        show("missing-command", code, text, "dummy_alive %s" % alive(dummy.pid))
        check(code == 2 and "launch_failed missing command" in text, "missing command returns 2")

        for label, value in (("negative", "-1"), ("zero", "0"), ("nan", "nan"), ("inf", "inf")):
            log_path = os.path.join(root, "timeout-%s.log" % label)
            code, text, _err = run_launcher(value, os.path.join(root, "absent-%s.json" % label), log_path, ["sleep", "30"])
            show("reject-" + label, code, text, "dummy_alive %s" % alive(dummy.pid))
            check(code == 2 and text.startswith("method posix-process-group") and "\npid " not in text and "rejected timeout" in text,
                  "rejects %s timeout before launch" % label)

        log_path = os.path.join(root, "race.log")
        code, text, _err = run_launcher(5, os.path.join(root, "absent-race.json"), log_path, [sys.executable, "-c", "raise SystemExit(0)"])
        show("fast-exit", code, text, "dummy_alive %s" % alive(dummy.pid))
        check(code == 0 and "timeout " not in text and alive(dummy.pid), "fast exit is not a timeout and the dummy survives")

        pidfile = os.path.join(root, "timeout-child.txt")
        log_path = os.path.join(root, "timeout.log")
        started = time.time()
        code, text, _err = run_launcher(1, os.path.join(root, "absent-timeout.json"), log_path, command_for(
            "import subprocess,sys,time; p=subprocess.Popen(['sleep','30']); open(sys.argv[1],'w').write(str(p.pid)); time.sleep(30)",
            pidfile))
        elapsed = time.time() - started
        grandchild = int(open(pidfile, "r").read().strip()) if os.path.isfile(pidfile) else 0
        time.sleep(0.2)
        show("timeout", code, text, "elapsed %.3f grandchild %s alive %s dummy_alive %s" % (elapsed, grandchild, alive(grandchild), alive(dummy.pid)))
        check(elapsed < 4 and code in (-15, 241) and "exit -15\n" in text and "stage not_run" in text and not alive(grandchild) and alive(dummy.pid),
              "timeout is bounded, records exit -15, and stops only that group")

        pidfile = os.path.join(root, "stubborn.txt")
        log_path = os.path.join(root, "stubborn.log")
        started = time.time()
        code, text, _err = run_launcher(1, os.path.join(root, "absent-stubborn.json"), log_path, command_for(
            "import subprocess,sys,time\n"
            "child=subprocess.Popen([sys.executable,'-c','import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(60)'])\n"
            "open(sys.argv[1],'w').write(str(child.pid)); time.sleep(60)\n",
            pidfile))
        elapsed = time.time() - started
        stubborn = int(open(pidfile, "r").read().strip()) if os.path.isfile(pidfile) else 0
        time.sleep(0.2)
        show("stubborn-child", code, text, "elapsed %.3f child %s alive %s dummy_alive %s" % (elapsed, stubborn, alive(stubborn), alive(dummy.pid)))
        check(elapsed < 5 and "15:group" in text and "9:group" in text and "exit -15\n" in text and not alive(stubborn) and alive(dummy.pid),
              "leader TERM exit still KILLs the same-group child that ignores TERM")

        pidfile = os.path.join(root, "ignore.log.pid")
        log_path = os.path.join(root, "ignore.log")
        code, text, _err = run_launcher(1, os.path.join(root, "absent-ignore.json"), log_path, [sys.executable, "-c",
            "import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(30)"])
        show("leader-ignores-term", code, text, "dummy_alive %s" % alive(dummy.pid))
        check(code in (-9, 247) and "exit -9\n" in text and "9:group" in text and alive(dummy.pid),
              "a leader that ignores TERM is stopped with KILL")

        pidfile = os.path.join(root, "interrupt.txt")
        log_path = os.path.join(root, "interrupt.log")
        proc = subprocess.Popen([sys.executable, LAUNCHER, "--timeout-seconds", "30", "--result", os.path.join(root, "absent-int.json"),
                                 "--wrapper-log", log_path, "--"] + command_for(
            "import subprocess,sys,time\n"
            "child=subprocess.Popen([sys.executable,'-c','import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(60)'])\n"
            "open(sys.argv[1],'w').write(str(child.pid)); time.sleep(60)\n",
            pidfile))
        for _ in range(40):
            if os.path.isfile(pidfile):
                break
            time.sleep(0.05)
        stubborn = int(open(pidfile, "r").read().strip()) if os.path.isfile(pidfile) else 0
        os.kill(proc.pid, signal.SIGINT)
        try:
            proc.wait(timeout=4)
            waited = "bounded"
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()
            waited = "exceeded"
        time.sleep(0.2)
        text = open(log_path, "r").read() if os.path.isfile(log_path) else ""
        show("keyboard-interrupt", proc.returncode, text, "wait %s child %s alive %s dummy_alive %s" % (waited, stubborn, alive(stubborn), alive(dummy.pid)))
        check(waited == "bounded" and proc.returncode not in (0, None) and "cleanup KeyboardInterrupt" in text and "exit 0\n" not in text
              and not alive(stubborn) and alive(dummy.pid),
              "KeyboardInterrupt cleans the owned group and is not reported as success")

        pidfile = os.path.join(root, "raised.txt")
        log_path = os.path.join(root, "raised.log")
        original_sleep = launcher.time.sleep
        state = {"n": 0}

        def explode(seconds):
            if state["n"]:
                return original_sleep(seconds)
            original_sleep(min(seconds, 0.05))
            if os.path.isfile(pidfile):
                state["n"] = 1
                raise RuntimeError("injected-failure")
            return None

        launcher.time.sleep = explode
        raised = None
        returned = "not-returned"
        try:
            returned = launcher.main(["--timeout-seconds", "30", "--result", os.path.join(root, "absent-raised.json"),
                                      "--wrapper-log", log_path, "--"] + command_for(
                "import subprocess,sys,time\n"
                "child=subprocess.Popen([sys.executable,'-c','import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(60)'])\n"
                "open(sys.argv[1],'w').write(str(child.pid)); time.sleep(60)\n",
                pidfile))
        except RuntimeError as error:
            raised = str(error)
        finally:
            launcher.time.sleep = original_sleep
        time.sleep(0.2)
        raised_pid = int(open(pidfile, "r").read().strip()) if os.path.isfile(pidfile) else 0
        text = open(log_path, "r").read() if os.path.isfile(log_path) else ""
        show("injected-exception", returned, text, "raised %s child %s alive %s dummy_alive %s" % (raised, raised_pid, alive(raised_pid), alive(dummy.pid)))
        check(raised == "injected-failure" and returned == "not-returned" and "cleanup RuntimeError" in text and "exit 0\n" not in text
              and not alive(raised_pid) and alive(dummy.pid),
              "an exception cleans the owned group and is not turned into success")

        print("linux_posix_group: exercised")
        print("mac_os: not_run")
        print("unity: not_run")
        print("checks %s" % CHECKS)
        print("dummy_final_alive %s" % alive(dummy.pid))
        return 0
    finally:
        if alive(dummy.pid):
            dummy.kill()
            dummy.wait()


if __name__ == "__main__":
    sys.exit(main())
