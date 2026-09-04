#!/usr/bin/env python3
"""
Listener for the Talon -> Unity gaze bridge.

Binds the UDP port the Unity receiver uses and prints a live one-line summary
of the stream: rate, validity, coordinates and the matched Unity window rect.

    python3 tools/gaze_monitor.py            # live status line
    python3 tools/gaze_monitor.py --raw      # one packet per line, verbatim
    python3 tools/gaze_monitor.py --port 5006

Only one process can receive on a UDP port, so the Unity player and this tool
cannot listen at the same time.
"""

import argparse
import json
import signal
import socket
import sys
import time

HOST = "127.0.0.1"
PORT = 5005


def _sigterm(signum, frame):
    raise KeyboardInterrupt


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--host", default=HOST)
    ap.add_argument("--port", type=int, default=PORT)
    ap.add_argument("--raw", action="store_true", help="print each packet")
    ap.add_argument(
        "--interval", type=float, default=0.25, help="status refresh seconds"
    )
    args = ap.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.bind((args.host, args.port))
    except OSError as e:
        print(f"cannot bind {args.host}:{args.port}: {e}", file=sys.stderr)
        print("is the Unity player or another monitor already listening?",
              file=sys.stderr)
        return 1
    sock.settimeout(0.2)
    signal.signal(signal.SIGTERM, _sigterm)  # print the summary on kill
    print(f"listening on {args.host}:{args.port}  (ctrl-c to stop)")

    count = 0
    valid = 0
    window_count = 0
    bad = 0
    max_gap = 0.0
    last_rx = None
    last_msg = None
    window_start = time.monotonic()
    window_frames = 0
    rate = 0.0
    last_print = 0.0

    try:
        while True:
            now = time.monotonic()
            try:
                data, _ = sock.recvfrom(2048)
            except socket.timeout:
                data = None
            except OSError as e:
                print(f"\nrecv error: {e}", file=sys.stderr)
                return 1

            if data is not None:
                if last_rx is not None:
                    max_gap = max(max_gap, now - last_rx)
                last_rx = now
                count += 1
                window_frames += 1
                try:
                    msg = json.loads(data.decode("utf-8"))
                except Exception:
                    bad += 1
                    continue
                last_msg = msg
                if msg.get("ok"):
                    valid += 1
                if msg.get("in"):
                    window_count += 1
                if args.raw:
                    print(data.decode("utf-8", "replace"))

            elapsed = now - window_start
            if elapsed >= 1.0:
                rate = window_frames / elapsed
                window_frames = 0
                window_start = now

            if not args.raw and now - last_print >= args.interval:
                last_print = now
                age = (now - last_rx) if last_rx is not None else float("inf")
                print("\r" + _status(last_msg, rate, count, valid, window_count,
                                     bad, max_gap, age).ljust(140),
                      end="", flush=True)
    except KeyboardInterrupt:
        print()
        _summary(count, valid, window_count, bad, max_gap)
        return 0


def _fmt_xy(msg, kx, ky):
    if msg is None or kx not in msg:
        return "  --.---,  --.---"
    return f"{msg[kx]:8.4f},{msg[ky]:8.4f}"


def _status(msg, rate, count, valid, window_count, bad, max_gap, age):
    if msg is None:
        return f"waiting for packets...  ({count} received)"
    ok = "OK " if msg.get("ok") else "---"
    eyes = msg.get("eyes", 0)
    eyestr = ("L" if eyes & 1 else "-") + ("R" if eyes & 2 else "-")
    inwin = "in " if msg.get("in") else "out"
    win = msg.get("win")
    winstr = (
        f"win[{win[0]:.0f},{win[1]:.0f} {win[2]:.0f}x{win[3]:.0f}]"
        if win else "win[none]"
    )
    validpct = (100.0 * valid / count) if count else 0.0
    return (
        f"{rate:5.1f}Hz  {ok} {eyestr}  "
        f"screen {_fmt_xy(msg, 'sx', 'sy')}  "
        f"window {_fmt_xy(msg, 'wx', 'wy')} {inwin}  "
        f"{winstr}  valid {validpct:5.1f}%  gap {max_gap*1000:5.1f}ms  "
        f"age {age*1000:6.1f}ms  bad {bad}"
    )


def _summary(count, valid, window_count, bad, max_gap):
    print(f"packets       : {count}")
    print(f"valid gaze    : {valid} ({100.0*valid/count:.1f}%)" if count else
          "valid gaze    : 0")
    print(f"inside window : {window_count}")
    print(f"unparseable   : {bad}")
    print(f"largest gap   : {max_gap*1000:.1f} ms")


if __name__ == "__main__":
    sys.exit(main())
