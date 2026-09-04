"""
Talon -> Unity gaze bridge (macOS, Tobii Eye Tracker).

Streams the gaze point Talon receives from the eye tracker to a local UDP
endpoint (default 127.0.0.1:5005) as one JSON object per datagram. Only
loopback addresses are used; no gaze data leaves the machine.

Install by symlinking this file into ~/.talon/user/. See README.md for the
packet format.

Coordinate convention:
    x = 0 -> left edge,  x = 1 -> right edge
    y = 0 -> top edge,   y = 1 -> bottom edge
Talon reports gaze in this convention already, normalized against the main
screen rect in macOS logical points (retina scale is not applied).
"""

import json
import math
import queue
import socket
import threading
import time

from talon import app, cron, ui

# --------------------------------------------------------------------------
# Configuration
# --------------------------------------------------------------------------

HOST = "127.0.0.1"
PORT = 5005

# 0 sends every frame the tracker produces (~90 Hz on a Tobii 4C); a positive value
# caps the send rate. Validity transitions are sent regardless of the cap.
MAX_RATE_HZ = 0

# Interval at which an "invalid gaze" packet is emitted while no frames are
# arriving, so a consumer can tell a running bridge from a stopped one.
HEARTBEAT_INTERVAL = 0.25

# Substrings matched case-insensitively against a window's app name, bundle id
# and executable path to locate the Unity window.
WINDOW_MATCH = ("unity",)

# Window titles to ignore, matched case-insensitively as substrings.
WINDOW_EXCLUDE = ("unity hub", "package manager", "console")

# Window rect refresh interval. Accessibility queries are too expensive to run
# per gaze frame.
WINDOW_POLL = "250ms"

# Interval at which Talon is re-checked for an attached tracker device.
TRACKER_POLL = "1s"

DEBUG = False

PROTOCOL_VERSION = 1

# Accepted range for raw gaze coordinates before clamping, matching the band
# Talon's own eye_zoom_mouse plugin uses.
_COORD_MIN = -0.02
_COORD_MAX = 1.02

# Talon reloads user files in place, leaving the previous load's callbacks,
# cron jobs and sender thread running. Each load stashes its shutdown function
# on the long-lived eye_mouse module so the next load can stop it first.
_STASH_ATTR = "_talon_unity_gaze_bridge_stop"

EYE_LEFT = 1
EYE_RIGHT = 2


def _log(*args):
    if DEBUG:
        print("[gaze_bridge]", *args)


def _warn(*args):
    print("[gaze_bridge]", *args)


# --------------------------------------------------------------------------
# Screen and window geometry
# --------------------------------------------------------------------------


class Geometry:
    """Main-screen and Unity-window rects in macOS logical points.

    Refreshed on the Talon main thread by a cron job and read by the sender
    thread through snapshot().
    """

    def __init__(self):
        self.lock = threading.Lock()
        self.screen = None      # (x, y, w, h)
        self.window = None      # (x, y, w, h), or None if no window matched
        self.window_title = ""
        self.job = None

    def start(self):
        self.refresh()
        if self.job is None:
            self.job = cron.interval(WINDOW_POLL, self.refresh)

    def stop(self):
        if self.job is not None:
            cron.cancel(self.job)
            self.job = None

    def refresh(self):
        screen = None
        window = None
        title = ""
        try:
            rect = ui.main_screen().rect
            screen = (rect.x, rect.y, rect.width, rect.height)
        except Exception as e:  # display change in progress
            _log("screen query failed:", e)
        try:
            win = self._find_window()
            if win is not None:
                rect = win.rect
                if rect.width > 1 and rect.height > 1:
                    window = (rect.x, rect.y, rect.width, rect.height)
                    title = win.title or ""
        except Exception as e:  # missing accessibility permission, app quitting
            _log("window query failed:", e)
        with self.lock:
            self.screen = screen
            self.window = window
            self.window_title = title

    @staticmethod
    def _matches(win) -> bool:
        try:
            app_ = win.app
            haystack = " ".join(
                str(v or "").lower()
                for v in (app_.name, app_.bundle, app_.exe, app_.path)
            )
        except Exception:
            return False
        if not any(m in haystack for m in WINDOW_MATCH):
            return False
        title = (win.title or "").lower()
        if any(x in title for x in WINDOW_EXCLUDE):
            return False
        return True

    def _find_window(self):
        """The focused Unity window, else the largest visible Unity window."""
        try:
            active = ui.active_window()
        except Exception:
            active = None
        if active is not None and self._matches(active):
            return active

        best = None
        best_area = 0.0
        for win in ui.windows():
            try:
                if win.hidden or not self._matches(win):
                    continue
                rect = win.rect
                area = rect.width * rect.height
            except Exception:
                continue
            if area > best_area:
                best, best_area = win, area
        return best

    def snapshot(self):
        with self.lock:
            return self.screen, self.window, self.window_title


geometry = Geometry()


# --------------------------------------------------------------------------
# UDP sender
# --------------------------------------------------------------------------


class Sender:
    """Owns the socket and the worker thread that encodes and sends packets."""

    def __init__(self):
        self.queue = queue.Queue(maxsize=8)
        self.thread = None
        self.sock = None
        self.running = False
        self.addr = (HOST, PORT)
        self.last_send = 0.0
        self.last_ok = None
        self.last_error = ""
        self.error_logged_at = 0.0
        # counters reported by bridge_status()
        self.frames = 0
        self.sent = 0
        self.dropped = 0
        self.invalid = 0

    def start(self):
        if self.running:
            return
        self.addr = (HOST, PORT)
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.setblocking(False)
        self.running = True
        self.thread = threading.Thread(
            target=self._run, name="gaze_bridge_sender", daemon=True
        )
        self.thread.start()

    def stop(self):
        if not self.running:
            return
        self.running = False
        try:
            self.queue.put_nowait(None)
        except queue.Full:
            pass
        thread, self.thread = self.thread, None
        if thread is not None:
            thread.join(timeout=1.0)
        sock, self.sock = self.sock, None
        if sock is not None:
            try:
                sock.close()
            except Exception:
                pass

    def submit(self, sample):
        """Queue a sample from the tracker callback thread without blocking."""
        self.frames += 1
        try:
            self.queue.put_nowait(sample)
        except queue.Full:
            # The newest sample supersedes the oldest queued one.
            try:
                self.queue.get_nowait()
                self.dropped += 1
                self.queue.put_nowait(sample)
            except (queue.Empty, queue.Full):
                self.dropped += 1

    # -- worker thread -----------------------------------------------------

    def _run(self):
        while self.running:
            try:
                sample = self.queue.get(timeout=HEARTBEAT_INTERVAL)
            except queue.Empty:
                self._emit(time.perf_counter(), False, 0, None)
                continue
            if sample is None:
                break
            try:
                self._handle(sample)
            except Exception as e:
                self._note_error(f"send failed: {e!r}")

    def _handle(self, sample):
        ts, ok, eyes, point = sample
        if not ok:
            self.invalid += 1
        if MAX_RATE_HZ > 0 and ok == self.last_ok:
            now = time.perf_counter()
            if now - self.last_send < 1.0 / MAX_RATE_HZ:
                return
        self._emit(ts, ok, eyes, point)

    def _emit(self, ts, ok, eyes, point):
        screen, window, title = geometry.snapshot()
        msg = {
            "v": PROTOCOL_VERSION,
            "t": round(ts, 6),
            "wall": round(time.time(), 6),
            "ok": 1 if ok else 0,
            "eyes": eyes,
        }
        sx = sy = None
        if point is not None:
            sx, sy = point
            msg["sx"] = round(sx, 5)
            msg["sy"] = round(sy, 5)
        if screen is not None:
            msg["scr"] = [round(v, 1) for v in screen]
        if window is not None:
            msg["win"] = [round(v, 1) for v in window]
            msg["title"] = title[:64]
            if sx is not None and screen is not None:
                px = screen[0] + sx * screen[2]
                py = screen[1] + sy * screen[3]
                wx = (px - window[0]) / window[2]
                wy = (py - window[1]) / window[3]
                msg["wx"] = round(wx, 5)
                msg["wy"] = round(wy, 5)
                msg["in"] = 1 if (0.0 <= wx <= 1.0 and 0.0 <= wy <= 1.0) else 0
            else:
                msg["in"] = 0

        payload = json.dumps(msg, separators=(",", ":")).encode("utf-8")
        try:
            self.sock.sendto(payload, self.addr)
            self.sent += 1
            self.last_send = time.perf_counter()
            self.last_ok = bool(ok)
        except (ConnectionRefusedError, BlockingIOError, OSError) as e:
            # No listener yet, or the send buffer is full; both are expected
            # for a fire-and-forget stream.
            self._note_error(f"{type(e).__name__}: {e}")

    def _note_error(self, text):
        self.last_error = text
        now = time.perf_counter()
        if DEBUG and now - self.error_logged_at > 5.0:
            self.error_logged_at = now
            _log(text)


sender = Sender()


# --------------------------------------------------------------------------
# Gaze source
# --------------------------------------------------------------------------


def _valid_point(p):
    """Return (x, y) clamped to [0, 1], or None if p is not usable."""
    if p is None:
        return None
    try:
        x, y = float(p.x), float(p.y)
    except Exception:
        return None
    if not (math.isfinite(x) and math.isfinite(y)):
        return None
    if not (_COORD_MIN <= x <= _COORD_MAX and _COORD_MIN <= y <= _COORD_MAX):
        return None
    return (min(max(x, 0.0), 1.0), min(max(y, 0.0), 1.0))


def on_gaze(frame):
    """Tracker callback. Runs on Talon's tracker thread; queues and returns."""
    try:
        ts = getattr(frame, "ts", None)
        if ts is None:
            ts = time.perf_counter()

        # An eye object is falsy when that eye is not being tracked.
        left = getattr(frame, "left", None)
        right = getattr(frame, "right", None)
        eyes = 0
        if left:
            eyes |= EYE_LEFT
        if right:
            eyes |= EYE_RIGHT

        # Use the tracker's combined estimate when it has one, otherwise
        # average whichever eyes reported a usable point.
        point = _valid_point(getattr(frame, "gaze", None))
        if point is None:
            lp = _valid_point(getattr(left, "gaze", None)) if left else None
            rp = _valid_point(getattr(right, "gaze", None)) if right else None
            if lp and rp:
                point = ((lp[0] + rp[0]) / 2.0, (lp[1] + rp[1]) / 2.0)
            else:
                point = lp or rp

        ok = point is not None and eyes != 0
        sender.submit((float(ts), ok, eyes, point))
    except Exception as e:  # an exception here would surface inside Talon
        _warn("on_gaze error:", repr(e))


class TrackerBinding:
    """Keeps on_gaze registered with whichever tracker Talon has attached."""

    def __init__(self):
        self.tracker = None
        self.job = None
        self.enabled = False
        self.module = None
        try:
            import talon_plugins.eye_mouse as eye_mouse

            self.module = eye_mouse
        except Exception as e:
            _warn("talon_plugins.eye_mouse unavailable:", repr(e))

    def start(self):
        self.enabled = True
        self._sync()
        if self.job is None:
            self.job = cron.interval(TRACKER_POLL, self._sync)

    def stop(self):
        self.enabled = False
        if self.job is not None:
            cron.cancel(self.job)
            self.job = None
        self._detach()

    def _current(self):
        if self.module is None:
            return None
        # Read the attribute on each call; the plugin rebinds this global as
        # trackers are attached and detached.
        return getattr(self.module, "tracker", None)

    def _sync(self):
        tracker = self._current()
        if tracker is self.tracker:
            return
        self._detach()
        if tracker is not None and self.enabled:
            try:
                tracker.register("gaze", on_gaze)
                self.tracker = tracker
                _warn("attached to tracker", tracker)
            except Exception as e:
                _warn("tracker register failed:", repr(e))

    def _detach(self):
        if self.tracker is not None:
            try:
                self.tracker.unregister("gaze", on_gaze)
            except Exception as e:
                _log("unregister failed:", e)
            self.tracker = None


binding = TrackerBinding()


# --------------------------------------------------------------------------
# Lifecycle
# --------------------------------------------------------------------------


def _shutdown_previous_load():
    """Stop a bridge left running by an earlier load of this file."""
    host = binding.module
    if host is None:
        return
    stop = getattr(host, _STASH_ATTR, None)
    if stop is None or stop is bridge_stop:
        return
    try:
        stop()
    except Exception as e:
        _log("previous load shutdown failed:", e)


def bridge_start():
    """Start streaming. Safe to call repeatedly."""
    _shutdown_previous_load()
    if binding.module is not None:
        setattr(binding.module, _STASH_ATTR, bridge_stop)
    geometry.start()
    sender.start()
    binding.start()
    _warn(f"streaming gaze to {HOST}:{PORT}")


def bridge_stop():
    """Unregister the gaze callback and shut down the sender."""
    binding.stop()
    sender.stop()
    geometry.stop()
    _warn("stopped")


def bridge_status() -> str:
    """Multi-line status report, for printing from the Talon REPL."""
    screen, window, title = geometry.snapshot()
    return "\n".join([
        f"gaze_bridge -> {HOST}:{PORT}",
        f"  tracker:  {binding.tracker!r}",
        f"  frames:   {sender.frames}  sent: {sender.sent}  "
        f"dropped: {sender.dropped}  invalid: {sender.invalid}",
        f"  screen:   {screen}",
        f"  window:   {window} {title!r}",
        f"  last err: {sender.last_error or 'none'}",
    ])


# "ready" does not fire again when the file is reloaded after startup, so a
# one-shot covers that case; bridge_start() is idempotent.
app.register("ready", lambda: bridge_start())
cron.after("250ms", bridge_start)
