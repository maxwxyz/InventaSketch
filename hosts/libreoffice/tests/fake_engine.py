"""Stand-in for the engine process in unit tests: speaks the JSON-lines protocol. Methods: echo (returns params),
fail (error envelope), sleep (waits params.seconds), crash (exits without answering), wrongId, garbage."""

import json
import sys
import time

for line in sys.stdin:
    if not line.strip():
        continue
    req = json.loads(line)
    method, params, rid = req.get("method"), req.get("params") or {}, req.get("id")
    if method == "exit":
        print(json.dumps({"id": rid, "ok": True, "result": None}), flush=True)
        break
    if method == "crash":
        sys.exit(3)
    if method == "sleep":
        time.sleep(float(params.get("seconds", 5)))
    if method == "garbage":
        print("not json", flush=True)
        continue
    if method == "wrongId":
        print(json.dumps({"id": -1, "ok": True, "result": None}), flush=True)
        continue
    if method == "fail":
        print(json.dumps({"id": rid, "ok": False, "error": "Unknown library item 'x'."}), flush=True)
        continue
    print(json.dumps({"id": rid, "ok": True, "result": params}), flush=True)
