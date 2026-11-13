#!/usr/bin/env python3
"""Run one command in its own session and stop only that process group on timeout.

The child is started with start_new_session, so its pid is the process-group id.
Timeout signalling uses os.killpg on that id. It does not list processes and
does not search for other Unity processes. A child that leaves this group is
outside the definite set and is not hunted down.

This does not start Unity by itself and does not claim packages were resolved.
If the result file is absent, the wrapper log says not_run.
Python 3.9 standard library is enough, including the Python 3.9.6 shipped with
the Xcode Command Line Tools. No pip packages are required.
"""
import argparse
import os
import signal
import subprocess
import sys
import time


def signal_own_group(pid, sig):
    """Signal the group led by pid, or only pid if it is no longer that leader."""
    try:
        pgid = os.getpgid(pid)
    except OSError:
        return "exited"
    if pgid != pid:
        try:
            os.kill(pid, sig)
        except OSError:
            return "exited"
        return "not-leader"
    try:
        os.killpg(pgid, sig)
    except OSError:
        return "exited"
    return "group"


def finish(log, code, result_path):
    reached = os.path.isfile(result_path) if result_path else False
    log.write("exit %s\nresult_present %s\n" % (code, "true" if reached else "false"))
    if not reached:
        log.write("stage not_run: executeMethod result file was not written\n")
    log.flush()
    return code


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--timeout-seconds", type=float, required=True)
    parser.add_argument("--result", required=True)
    parser.add_argument("--wrapper-log", required=True)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args(argv)
    command = args.command
    if command and command[0] == "--":
        command = command[1:]
    log = open(args.wrapper_log, "w")
    code = 1
    try:
        log.write("method posix-process-group\nreads_proc false\n")
        if not command:
            log.write("launch_failed missing command\n")
            code = finish(log, 2, args.result)
            return code
        try:
            process = subprocess.Popen(command, start_new_session=True)
        except OSError as error:
            log.write("launch_failed %s\n" % error)
            code = finish(log, 127, args.result)
            return code
        log.write("pid %s\n" % process.pid)
        try:
            log.write("pgid %s\n" % os.getpgid(process.pid))
        except OSError:
            log.write("pgid unknown\n")
        log.flush()
        deadline = time.time() + args.timeout_seconds
        while process.poll() is None and time.time() < deadline:
            time.sleep(0.05)
        if process.poll() is None:
            notes = []
            for sig in (signal.SIGTERM, signal.SIGKILL):
                if process.poll() is not None:
                    break
                notes.append("%s:%s" % (sig, signal_own_group(process.pid, sig)))
                for _ in range(10):
                    if process.poll() is not None:
                        break
                    time.sleep(0.05)
            if process.poll() is None:
                process.wait()
            log.write("timeout %s\n" % " ".join(notes))
        code = process.returncode if process.returncode is not None else 124
        code = finish(log, code, args.result)
        return code
    finally:
        log.flush()
        log.close()


if __name__ == "__main__":
    sys.exit(main())
