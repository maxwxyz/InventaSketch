"""Runs extension commands on LibreOffice's main thread.

Commands dispatched over the remote UNO bridge (scripts, tests) arrive on a bridge thread. Drawing works there, but UI
work (opening the sidebar deck, creating panel windows) from a foreign thread can crash soffice. Commands from a
foreign thread are therefore posted to the main thread (com.sun.star.awt.AsyncCallback) and the caller waits for them;
menu, sidebar and macros already call on the main thread. The main thread is the thread in which an AsyncCallback runs
(``learn``); until it is known, commands run on the calling thread (never wait on the main thread for itself).

Ported from the Slidemax LibreOffice host (hauptthread.py), translated and simplified. Testable without UNO.
"""

import threading
import time

MAIN = {"id": None, "asked": False}
# Seconds a foreign thread waits for the main thread to start a command (it never starts while LibreOffice quits).
START_LIMIT = 30.0


def learn(post):
    """Determines the main thread once: post(work) runs work on the main thread."""
    if MAIN["id"] is not None or MAIN["asked"]:
        return
    MAIN["asked"] = True

    def remember():
        MAIN["id"] = threading.get_ident()
    try:
        post(remember)
    except Exception:
        MAIN["asked"] = False


def foreign():
    """True if the calling code is known not to run on the main thread."""
    return MAIN["id"] is not None and threading.get_ident() != MAIN["id"]


def call(work, post, start_limit=None):
    """Runs work() on the main thread and returns its result (directly if already there or the main thread is unknown).
    Exceptions reach the caller. If the main thread does not start within start_limit seconds, work is cancelled and
    None is returned."""
    if not foreign():
        return work()
    done = threading.Event()
    result = {}
    state = {"started": False, "cancelled": False}
    lock = threading.Lock()

    def run():
        with lock:
            if state["cancelled"]:
                return
            state["started"] = True
        try:
            result["value"] = work()
        except BaseException as ex:  # handed to the caller
            result["error"] = ex
        finally:
            done.set()
    post(run)
    limit = START_LIMIT if start_limit is None else start_limit
    began = time.time()
    while not done.wait(0.25):
        with lock:
            if not state["started"] and time.time() - began > limit:
                state["cancelled"] = True
                return None
    if "error" in result:
        raise result["error"]
    return result.get("value")


def poster(ctx):
    """post(work) via com.sun.star.awt.AsyncCallback (LibreOffice only)."""
    import unohelper
    from com.sun.star.awt import XCallback

    from .log import log

    class _Call(unohelper.Base, XCallback):
        def __init__(self, work):
            self.work = work

        def notify(self, data):
            try:
                self.work()
            except Exception:
                import traceback
                log("main thread callback: " + traceback.format_exc())
            finally:
                self.work = None

    def post(work):
        ctx.ServiceManager.createInstanceWithContext("com.sun.star.awt.AsyncCallback", ctx).addCallback(_Call(work), None)
    return post
