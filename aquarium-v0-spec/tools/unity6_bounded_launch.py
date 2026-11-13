#!/usr/bin/env python3
"""Run one command in its own process group and stop only that group on timeout.

This does not start Unity by itself and does not claim packages were resolved.
If the command never reaches an executeMethod, the result file stays absent and
the stage remains not_run.
"""
import argparse
import os
import signal
import subprocess
import sys
import time


def descendants(pid):
    found = []
    for name in os.listdir("/proc"):
        if not name.isdigit():
            continue
        try:
            with open("/proc/%s/stat" % name) as handle:
                stat = handle.read()
        except OSError:
            continue
        right = stat.rfind(")")
        fields = stat[right + 2:].split()
        if len(fields) > 1 and fields[1] == str(pid):
            child = int(name)
            found.append(child)
            found.extend(descendants(child))
    return found


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--timeout-seconds", type=float, required=True)
    parser.add_argument("--result", required=True)
    parser.add_argument("--wrapper-log", required=True)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.command and args.command[0] == "--":
        args.command = args.command[1:]
    if not args.command:
        print("missing command", file=sys.stderr)
        return 2
    log = open(args.wrapper_log, "w")
    started = time.time()
    process = subprocess.Popen(args.command, start_new_session=True)
    log.write("pid %s\n" % process.pid)
    log.flush()
    deadline = started + args.timeout_seconds
    while process.poll() is None and time.time() < deadline:
        time.sleep(0.2)
    timed_out = process.poll() is None
    if timed_out:
        targets = [process.pid] + descendants(process.pid)
        for sig in (signal.SIGTERM, signal.SIGKILL):
            for pid in targets:
                try:
                    os.kill(pid, sig)
                except OSError:
                    pass
            time.sleep(0.2)
        process.wait()
        log.write("timeout killed %s\n" % " ".join(str(pid) for pid in targets))
    code = process.returncode if process.returncode is not None else 124
    reached = os.path.isfile(args.result)
    log.write("exit %s\nresult_present %s\n" % (code, "true" if reached else "false"))
    if not reached:
        log.write("stage not_run: executeMethod result file was not written\n")
    log.close()
    return code


if __name__ == "__main__":
    sys.exit(main())
