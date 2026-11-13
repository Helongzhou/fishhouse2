#!/usr/bin/env python3
"""Run one command in its own session and stop only that process group.

start_new_session makes the child the process-group leader. The launcher
records that pgid once, after checking it is the child's pid and not the
parent group. Later signals use the saved pgid, including after the leader
has exited, so a same-group child that ignores SIGTERM still receives SIGKILL.
Waits are bounded. Processes that leave the group are not searched for.

Ctrl+C and other failures use the same bounded stop, then keep the original
exception. This does not start Unity or claim that packages were resolved.
Python 3.9 standard library is enough. No pip packages are required.
"""
import argparse
import math
import os
import signal
import subprocess
import sys
import time

STOP_SLICE_SECONDS = 0.5


def signal_saved_group(pgid, sig):
    """Signal one saved process group. Never signal the caller's group."""
    if isinstance(pgid, bool) or not isinstance(pgid, int) or pgid <= 1 or pgid == os.getpgrp():
        return "refused"
    try:
        os.killpg(pgid, sig)
    except OSError:
        return "exited"
    return "group"


def group_empty(pgid):
    return signal_saved_group(pgid, 0) != "group"


def bounded_stop(pgid, leader):
    """TERM then, if the group still exists, KILL. Neither wait is open-ended."""
    notes = []
    for sig in (signal.SIGTERM, signal.SIGKILL):
        notes.append("%s:%s" % (int(sig), signal_saved_group(pgid, sig)))
        deadline = time.time() + STOP_SLICE_SECONDS
        while time.time() < deadline:
            if leader is not None and leader.poll() is None:
                try:
                    leader.wait(timeout=0.05)
                except subprocess.TimeoutExpired:
                    pass
            if group_empty(pgid):
                break
            time.sleep(0.05)
        if group_empty(pgid):
            break
    if leader is not None and leader.poll() is None:
        try:
            leader.wait(timeout=STOP_SLICE_SECONDS)
        except subprocess.TimeoutExpired:
            pass
    return notes


def finish(log, code, result_path):
    reached = bool(result_path) and os.path.isfile(result_path)
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
    command = list(args.command)
    if command and command[0] == "--":
        command = command[1:]
    log = open(args.wrapper_log, "w")
    owned_pgid = None
    process = None
    stopped = False
    code = 1

    def ensure_stop(reason):
        nonlocal stopped
        if stopped or owned_pgid is None:
            return
        stopped = True
        previous = signal.signal(signal.SIGINT, signal.SIG_IGN)
        try:
            notes = bounded_stop(owned_pgid, process)
        finally:
            signal.signal(signal.SIGINT, previous)
        log.write("%s %s\n" % (reason, " ".join(notes)))
        log.flush()

    try:
        log.write("method posix-process-group\nreads_proc false\n")
        if not math.isfinite(args.timeout_seconds) or args.timeout_seconds <= 0:
            log.write("rejected timeout %r\n" % (args.timeout_seconds,))
            code = finish(log, 2, args.result)
            return code
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
        try:
            seen = os.getpgid(process.pid)
        except OSError:
            seen = None
        if seen != process.pid or seen == os.getpgrp() or seen <= 1:
            log.write("pgid rejected pid %s seen %s\n" % (process.pid, seen))
            if process.poll() is None:
                try:
                    os.kill(process.pid, signal.SIGKILL)
                except OSError:
                    pass
                try:
                    process.wait(timeout=STOP_SLICE_SECONDS)
                except subprocess.TimeoutExpired:
                    pass
            code = finish(log, 127, args.result)
            return code
        owned_pgid = seen
        log.write("pid %s\npgid %s\n" % (process.pid, owned_pgid))
        log.flush()
        deadline = time.time() + args.timeout_seconds
        while process.poll() is None and time.time() < deadline:
            time.sleep(0.05)
        if process.poll() is None:
            ensure_stop("timeout")
        elif not group_empty(owned_pgid):
            ensure_stop("leader-exited")
        code = process.returncode if process.returncode is not None else 124
        code = finish(log, code, args.result)
        return code
    except BaseException as error:
        ensure_stop("cleanup %s" % type(error).__name__)
        if not os.path.isfile(args.result):
            log.write("stage not_run: executeMethod result file was not written\n")
            log.flush()
        raise
    finally:
        if owned_pgid is not None and not stopped and not group_empty(owned_pgid):
            ensure_stop("cleanup")
        log.flush()
        log.close()


if __name__ == "__main__":
    sys.exit(main())
