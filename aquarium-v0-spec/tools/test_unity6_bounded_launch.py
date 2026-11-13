#!/usr/bin/env python3
"""Cloud checks for the POSIX process-group launcher. This is not a Mac or Unity run."""
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


def check(condition, name):
    global CHECKS
    if not condition:
        raise SystemExit("FAIL " + name)
    print("PASS " + name)
    CHECKS += 1


def run_launcher(timeout, result, log_path, command):
    completed = subprocess.run(
        [sys.executable, LAUNCHER, "--timeout-seconds", str(timeout), "--result", result, "--wrapper-log", log_path, "--"] + command,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    text = open(log_path, "r").read() if os.path.isfile(log_path) else ""
    return completed.returncode, text, completed.stderr.decode("utf-8", "replace")


def alive(pid):
    try:
        os.kill(pid, 0)
    except OSError:
        return False
    return True


def main():
    source = open(LAUNCHER, "r").read()
    check("/proc" not in source, "launcher source does not read /proc")
    root = tempfile.mkdtemp(prefix="aquarium-launch-")
    dummy = subprocess.Popen(["sleep", "120"])
    try:
        dead = subprocess.Popen(["sleep", "0.05"], start_new_session=True)
        dead.wait()
        check(launcher.signal_own_group(dead.pid, signal.SIGTERM) == "exited", "signal after exit does not raise")
        follower = subprocess.Popen(["sleep", "30"])
        check(os.getpgid(follower.pid) != follower.pid, "non-leader is not its own group")
        check(launcher.signal_own_group(follower.pid, 0) == "not-leader", "non-leader is signalled alone")
        check(alive(follower.pid) and alive(dummy.pid), "signal 0 leaves the follower and the dummy alive")
        follower.terminate()
        follower.wait()

        result = os.path.join(root, "ok.json")
        log_path = os.path.join(root, "ok.log")
        code, text, _err = run_launcher(5, result, log_path, [sys.executable, "-c",
            "open(%r,'w').write('ok'); raise SystemExit(0)" % result])
        check(code == 0 and "result_present true" in text and "not_run" not in text, "success keeps exit 0 and does not say not_run")
        check("method posix-process-group" in text and "reads_proc false" in text, "log records the posix group method")

        log_path = os.path.join(root, "fail.log")
        code, text, _err = run_launcher(5, os.path.join(root, "missing.json"), log_path, [sys.executable, "-c", "raise SystemExit(3)"])
        check(code == 3 and "exit 3" in text and "result_present false" in text and "stage not_run" in text, "failure exit 3 with missing result stays not_run")

        log_path = os.path.join(root, "launch.log")
        code, text, _err = run_launcher(5, os.path.join(root, "missing.json"), log_path, ["/no/such/aquarium-unity"])
        check(code == 127 and "launch_failed" in text and os.path.isfile(log_path), "missing executable is a launch failure and keeps the log")

        log_path = os.path.join(root, "empty.log")
        code, text, _err = run_launcher(5, os.path.join(root, "missing.json"), log_path, [])
        check(code == 2 and "launch_failed missing command" in text, "missing command returns 2")

        log_path = os.path.join(root, "race.log")
        code, text, _err = run_launcher(5, os.path.join(root, "missing.json"), log_path, [sys.executable, "-c", "raise SystemExit(0)"])
        check(code == 0 and "timeout" not in text and alive(dummy.pid), "fast exit is not a timeout and the dummy survives")

        child_pid = os.path.join(root, "child.pid")
        log_path = os.path.join(root, "timeout.log")
        code, text, _err = run_launcher(1, os.path.join(root, "missing.json"), log_path, [sys.executable, "-c",
            "import subprocess,sys,time; p=subprocess.Popen(['sleep','30']); open(sys.argv[1],'w').write(str(p.pid)); time.sleep(30)" , child_pid])
        for _ in range(20):
            if os.path.isfile(child_pid):
                break
            time.sleep(0.05)
        grandchild = int(open(child_pid, "r").read().strip())
        time.sleep(0.2)
        check(code in (-15, 241) and "timeout" in text and "group" in text, "timeout returns the signal status")
        check("exit -15" in text and "stage not_run" in text, "timeout log keeps child exit -15 and not_run")
        check(not alive(grandchild) and alive(dummy.pid), "timeout stops the group child and not the dummy")

        log_path = os.path.join(root, "kill.log")
        code, text, _err = run_launcher(1, os.path.join(root, "missing.json"), log_path, [sys.executable, "-c",
            "import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(30)"])
        check(code in (-9, 247) and "exit -9" in text and alive(dummy.pid), "ignored SIGTERM is followed by SIGKILL only for that group")
        print("posix_process_group_without_proc: exercised")
        print("mac_os: not_run")
        print("unity: not_run")
        print("checks %s" % CHECKS)
        return 0
    finally:
        if alive(dummy.pid):
            dummy.kill()
            dummy.wait()


if __name__ == "__main__":
    sys.exit(main())
