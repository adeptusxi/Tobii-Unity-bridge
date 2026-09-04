#!/usr/bin/env python3
"""
Offline self-test for talon/gaze_bridge.py.

Loads the bridge against stub `talon` and `talon_plugins.eye_mouse` modules,
feeds it synthetic gaze frames and asserts the UDP packets it emits. Requires
neither Talon nor an eye tracker.

    python3 tools/test_gaze_bridge.py [-v] [path/to/gaze_bridge.py]
"""

import importlib.util
import json
import os
import socket
import sys
import time
import types

BRIDGE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "talon", "gaze_bridge.py",
)

# Not the bridge's default port, so a bridge running under Talon cannot feed
# packets into this test.
PORT = 5099

SCREEN = (0, 0, 1728, 1117)
WINDOW = (100, 50, 800, 600)


# --------------------------------------------------------------------------
# Stub Talon modules
# --------------------------------------------------------------------------


class _Rect:
    def __init__(self, x, y, w, h):
        self.x, self.y, self.width, self.height = x, y, w, h


class _Screen:
    rect = _Rect(*SCREEN)


class _App:
    name = "Unity"
    bundle = "com.unity3d.UnityEditor"
    exe = "/Applications/Unity"
    path = "/Applications/Unity"


class _Window:
    app = _App()
    title = "MyGame"
    hidden = False
    rect = _Rect(*WINDOW)


def _install_stubs():
    ui = types.ModuleType("talon.ui")
    ui.main_screen = lambda: _Screen()
    ui.active_window = lambda: _Window()
    ui.windows = lambda: [_Window()]

    # Cron jobs are never fired; the test calls refresh paths directly.
    cron = types.ModuleType("talon.cron")
    cron.interval = lambda spec, fn: object()
    cron.after = lambda spec, fn: None
    cron.cancel = lambda job: None

    app = types.ModuleType("talon.app")
    app.register = lambda name, fn: None

    talon = types.ModuleType("talon")
    talon.ui, talon.cron, talon.app = ui, cron, app

    eye_mouse = types.ModuleType("talon_plugins.eye_mouse")
    eye_mouse.tracker = _Tracker()
    plugins = types.ModuleType("talon_plugins")
    plugins.eye_mouse = eye_mouse

    sys.modules.update({
        "talon": talon,
        "talon.ui": ui,
        "talon.cron": cron,
        "talon.app": app,
        "talon_plugins": plugins,
        "talon_plugins.eye_mouse": eye_mouse,
    })
    return eye_mouse


class _Tracker:
    """Stands in for a Talon tobii.TobiiEC device."""

    def __init__(self):
        self.callbacks = {}

    def register(self, name, cb):
        self.callbacks.setdefault(name, []).append(cb)

    def unregister(self, name, cb):
        self.callbacks.get(name, []).remove(cb)

    def __repr__(self):
        return "<FakeTracker>"


# --------------------------------------------------------------------------
# Synthetic gaze frames
# --------------------------------------------------------------------------


class Point:
    def __init__(self, x, y):
        self.x, self.y = x, y


class Eye:
    """Talon eye objects are falsy when that eye is not tracked."""

    def __init__(self, gaze, tracked=True):
        self.gaze = gaze
        self._tracked = tracked

    def __bool__(self):
        return self._tracked


class Frame:
    def __init__(self, gaze, left, right, ts=1.0):
        self.gaze, self.left, self.right, self.ts = gaze, left, right, ts


def main() -> int:
    eye_mouse = _install_stubs()

    path = next((a for a in sys.argv[1:] if not a.startswith("-")), BRIDGE)
    spec = importlib.util.spec_from_file_location("gaze_bridge", path)
    gb = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gb)
    gb.PORT = PORT

    rx = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    rx.bind(("127.0.0.1", PORT))
    rx.settimeout(2.0)

    def recv():
        return json.loads(rx.recv(4096).decode())

    gb.bridge_start()
    time.sleep(0.1)
    gaze = eye_mouse.tracker.callbacks["gaze"]
    assert gaze == [gb.on_gaze], gaze
    cb = gaze[0]
    packets = {}

    # The tracker's combined estimate is used when present, and is mapped into
    # the window rect: px = 0.5 * 1728 = 864, py = 0.25 * 1117 = 279.25,
    # so wx = (864 - 100) / 800 and wy = (279.25 - 50) / 600.
    cb(Frame(Point(0.5, 0.25), Eye(Point(0.49, 0.25)), Eye(Point(0.51, 0.25))))
    m = packets["combined"] = recv()
    assert m["ok"] == 1 and m["eyes"] == 3
    assert abs(m["sx"] - 0.5) < 1e-6 and abs(m["sy"] - 0.25) < 1e-6
    assert abs(m["wx"] - 0.955) < 1e-4, m
    assert abs(m["wy"] - 0.382083) < 1e-4, m
    assert m["in"] == 1 and m["win"] == list(WINDOW) and m["title"] == "MyGame"

    # No combined estimate: both eyes are averaged.
    cb(Frame(None, Eye(Point(0.2, 0.2)), Eye(Point(0.4, 0.6))))
    m = packets["averaged"] = recv()
    assert abs(m["sx"] - 0.3) < 1e-6 and abs(m["sy"] - 0.4) < 1e-6, m

    # One eye tracked: its point is used as-is.
    cb(Frame(None, Eye(Point(0.8, 0.1)), Eye(None, tracked=False)))
    m = packets["one_eye"] = recv()
    assert m["ok"] == 1 and m["eyes"] == 1 and abs(m["sx"] - 0.8) < 1e-6, m

    # Neither eye tracked: invalid, no coordinates.
    cb(Frame(None, Eye(None, tracked=False), Eye(None, tracked=False)))
    m = packets["no_eyes"] = recv()
    assert m["ok"] == 0 and m["eyes"] == 0 and "sx" not in m, m

    # A gaze point with no tracked eyes is invalid and carries no coordinates.
    cb(Frame(Point(0.0, 0.0), Eye(None, tracked=False), Eye(None, tracked=False)))
    m = packets["untracked_with_point"] = recv()
    assert m["ok"] == 0 and m["eyes"] == 0 and "sx" not in m, m

    # NaN and out-of-range coordinates are rejected.
    cb(Frame(Point(float("nan"), 0.5), Eye(Point(1.5, 0.5)),
             Eye(None, tracked=False)))
    m = packets["rejected"] = recv()
    assert m["ok"] == 0, m

    # Valid gaze that falls outside the window rect.
    cb(Frame(Point(0.01, 0.99), Eye(Point(0.01, 0.99)), Eye(Point(0.01, 0.99))))
    m = packets["outside_window"] = recv()
    assert m["ok"] == 1 and m["in"] == 0, m

    # An idle tracker still produces heartbeat packets.
    t0 = time.time()
    m = packets["heartbeat"] = recv()
    assert m["ok"] == 0, m
    assert 0.15 < time.time() - t0 < 0.6, time.time() - t0

    # A malformed frame must not raise into Talon's tracker thread.
    cb(object())
    packets["malformed_frame"] = recv()

    gb.bridge_stop()
    assert eye_mouse.tracker.callbacks["gaze"] == []

    if "-v" in sys.argv:
        print(json.dumps(packets, indent=1))
    print(f"frames={gb.sender.frames} sent={gb.sender.sent} "
          f"dropped={gb.sender.dropped} invalid={gb.sender.invalid}")
    print("ALL CHECKS PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
