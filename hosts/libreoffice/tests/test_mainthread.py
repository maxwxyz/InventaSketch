"""Unit tests of the main-thread marshalling (a worker thread plays LibreOffice's main thread)."""

import os
import queue
import sys
import threading
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oxt", "pythonpath"))

from inventasketch_lo import mainthread  # noqa: E402


class FakeMain:
    def __init__(self):
        self.jobs = queue.Queue()
        self.thread = threading.Thread(target=self.loop, daemon=True)
        self.thread.start()

    def loop(self):
        while True:
            job = self.jobs.get()
            if job is None:
                return
            job()

    def post(self, work):
        self.jobs.put(work)


class MainThread(unittest.TestCase):
    def setUp(self):
        mainthread.MAIN.update(id=None, asked=False)
        self.main = FakeMain()

    def tearDown(self):
        self.main.jobs.put(None)
        mainthread.MAIN.update(id=None, asked=False)

    def learned(self):
        mainthread.learn(self.main.post)
        done = threading.Event()
        self.main.post(done.set)
        done.wait(5)

    def test_unknown_main_runs_directly(self):
        self.assertEqual(mainthread.call(threading.get_ident, self.main.post), threading.get_ident())

    def test_foreign_call_runs_on_main(self):
        self.learned()
        self.assertTrue(mainthread.foreign())
        self.assertEqual(mainthread.call(threading.get_ident, self.main.post), self.main.thread.ident)

    def test_errors_reach_the_caller(self):
        self.learned()

        def fail():
            raise ValueError("boom")
        with self.assertRaises(ValueError):
            mainthread.call(fail, self.main.post)

    def test_cancel_when_main_does_not_start(self):
        self.learned()
        ran = []
        result = mainthread.call(lambda: ran.append(1), lambda work: None, start_limit=0.3)
        self.assertIsNone(result)
        self.assertEqual(ran, [])


if __name__ == "__main__":
    unittest.main()
