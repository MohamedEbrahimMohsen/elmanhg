"""Move a finished story from "Remaining" to "Finished" in PROGRESS.md.

Usage: python scripts/progress_done.py <issue> <pr> <review_rounds> "<coderabbit>" "<follow-up>" "<main-sha>"
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
issue, pr, rounds, cr, follow, sha = sys.argv[1:7]
p = ROOT / "PROGRESS.md"
s = p.read_text(encoding="utf-8")

row = re.search(rf"^\| [^|]+ \| #{issue} \| (.+?) \|\s*$", s, re.M)
if not row:
    sys.exit(f"#{issue} not found in Remaining")
title = row.group(1).strip()
s = s[: row.start()] + s[row.end() + 1:]

fin = re.search(r"## Finished stories \((\d+) of (\d+)\)", s)
done, total = int(fin.group(1)) + 1, int(fin.group(2))
s = s.replace(fin.group(0), f"## Finished stories ({done} of {total})")
rem = re.search(r"## Remaining stories \((\d+)\)", s)
s = s.replace(rem.group(0), f"## Remaining stories ({int(rem.group(1)) - 1})")

rows = list(re.finditer(r"^\| (\d+) \| #\d+ \[.*\|\s*$", s, re.M))  # finished rows: "#n [Ek.Sm]" in one cell
last = rows[-1]
new = f"| {done} | #{issue} {title} | #{pr} | {rounds} | {cr} | {follow} |"
s = s[: last.end()] + "\n" + new + s[last.end():]

s = re.sub(r"^Last updated: .*$",
           f"Last updated: laptop session, after story #{issue} merged (main at `{sha}`).",
           s, count=1, flags=re.M)
s = re.sub(r"(`deferred`: [^·]*?)( ·)", lambda m: m.group(1) + (f", {follow}" if follow.startswith("#") and follow not in m.group(1) else "") + m.group(2), s, count=1)
nxt = re.search(r"^\| [^|]+ \| (#\d+) \| (.+?) \|\s*$", s, re.M)
if nxt:
    s = re.sub(r"^\*\*Next story: .*$", f"**Next story: {nxt.group(1)} {nxt.group(2).strip()}**, the first row of \"Remaining stories\".", s, count=1, flags=re.M)
p.write_text(s, encoding="utf-8")
print(f"OK {done}/{total}")
