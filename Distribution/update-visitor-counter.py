#!/usr/bin/env python3
import datetime as dt
import gzip
import json
import os
import re
from pathlib import Path

LOG_DIR = Path("/var/log/nginx")
OUTPUT = Path("/var/www/html/counter.json")
HISTORY = Path("/home/ubuntu/onharu-stats-history.json")
HIT = re.compile(r'\[([^]]+)\] "GET /(visit|download-hit)\.gif(?:\?([^ ]*))? HTTP/[^\"]+" 2\d\d [^\"]+ "[^\"]*" "([^"]*)"')
BOT = re.compile(r"bot|crawler|spider|preview|headless|monitor", re.I)
KST = dt.timezone(dt.timedelta(hours=9))


def lines(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8", errors="ignore") as stream:
        yield from stream


today = dt.datetime.now(KST).date().isoformat()
scanned = {}
for path in sorted(LOG_DIR.glob("access.log*")):
    if not path.is_file():
        continue
    for line in lines(path):
        match = HIT.search(line)
        if not match or BOT.search(match.group(4)):
            continue
        stamp = dt.datetime.strptime(match.group(1).split()[0], "%d/%b/%Y:%H:%M:%S").replace(tzinfo=dt.timezone.utc)
        day = stamp.astimezone(KST).date().isoformat()
        counts = scanned.setdefault(day, {"visits": 0, "setup": 0, "portable": 0})
        if match.group(2) == "visit":
            counts["visits"] += 1
        elif "file=setup" in (match.group(3) or ""):
            counts["setup"] += 1
        elif "file=portable" in (match.group(3) or ""):
            counts["portable"] += 1

history = json.loads(HISTORY.read_text(encoding="utf-8")) if HISTORY.exists() else {}
history.update(scanned)
history_tmp = HISTORY.with_suffix(".tmp")
history_tmp.write_text(json.dumps(history, ensure_ascii=False, indent=2, sort_keys=True), encoding="utf-8")
history_tmp.replace(HISTORY)
os.chmod(HISTORY, 0o600)

total = sum(day["visits"] for day in history.values())
setup = sum(day["setup"] for day in history.values())
portable = sum(day["portable"] for day in history.values())
payload = {"total": total, "today": history.get(today, {}).get("visits", 0), "updated": dt.datetime.now(KST).isoformat(timespec="minutes")}
temporary = OUTPUT.with_suffix(".tmp")
temporary.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
temporary.replace(OUTPUT)
os.chmod(OUTPUT, 0o644)
