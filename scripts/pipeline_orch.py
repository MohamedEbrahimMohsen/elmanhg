"""Orchestrator helper for the Elmanhg autopilot run (mechanical steps only).

python orch.py start <story>          pre-flight, branch, .process/<n>-<slug>/ with 00-acceptance, 00-story, 04-metrics
python orch.py verify <story>         build + test every stack folder that exists; prints PASS/FAIL per stack
python orch.py pr <story>             commit everything, push, open PR; prints PR number
python orch.py poll <story> <pr>      wait for CodeRabbit (<=15 min); writes 05-coderabbit-comments.md; prints ACTIONABLE=<n>
python orch.py push <story> <msg>     commit + push follow-up changes on the story branch
python orch.py merge <story> <pr>     squash merge, sync main, close story if still open
python orch.py issue <label> <title> <body-file> [story]   open an issue (creates label if needed); prints URL
python orch.py metric <story> <row>   append a metrics row (pipe-separated cells)
python orch.py sync                   switch to main and fast-forward (after a merge done outside this script)

Without the gh CLI (e.g. a Claude Code cloud session) the steps that talk to GitHub degrade: `start` reads the
issue from the public REST API, `pr` pushes and writes the PR body to a temp file, `merge` only pushes the
remaining artifacts; the orchestrator then opens, polls, merges and closes through its GitHub tools.
"""
import json, os, re, shutil, subprocess, sys, tempfile, time, datetime, pathlib, urllib.request

REPO = "MohamedEbrahimMohsen/elmanhg"
ROOT = pathlib.Path(__file__).resolve().parent.parent
HAS_GH = shutil.which("gh") is not None
ENV = dict(os.environ, GCM_INTERACTIVE="never", GIT_TERMINAL_PROMPT="0")
AUTOPILOT = ("Dev instruction (2026-09-27): \"this session will be ran once to implement EVERYTHING in this github project "
             "https://github.com/users/MohamedEbrahimMohsen/projects/1 ... You will do the same cycle story after story ... till you "
             "finish without any interruption. don't stop untill you finalized the project, whatever you stuck in it, ignore it and "
             "generate an ouput report at the end + put them as open issues in GitHub.\" Followed by: \"GO\". "
             "Continued in a cloud session (2026-09-28): \"this should be a very long session, with no stop till you finalize "
             "everything\"; the dev approved full autopilot (per-story branch, PR, squash-merge on green CI) for that session.")
TRAILER = "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" + (f"\nClaude-Session: {os.environ['CLAUDE_SESSION_URL']}" if os.environ.get("CLAUDE_SESSION_URL") else "")


def run(*args, check=True, capture=True, cwd=ROOT):
    r = subprocess.run(list(args), cwd=cwd, env=ENV, capture_output=capture, text=True, encoding="utf-8", errors="replace")
    if check and r.returncode != 0:
        raise SystemExit(f"FAILED: {' '.join(args)}\n{r.stdout}\n{r.stderr}")
    return r


def now():
    return datetime.datetime.now().strftime("%Y-%m-%d %H:%M")


def story(n):
    if HAS_GH:
        d = json.loads(run("gh", "issue", "view", str(n), "--repo", REPO, "--json", "number,title,body,state").stdout)
    else:
        with urllib.request.urlopen(f"https://api.github.com/repos/{REPO}/issues/{n}", timeout=30) as r:
            j = json.load(r)
        d = {"number": j["number"], "title": j["title"], "body": j["body"] or "", "state": j["state"].upper()}
    m = re.match(r"\[(E\d+)\.S(\d+)\]\s*(.*)", d["title"])
    key = f"{m.group(1)}.S{m.group(2)}"
    slug = re.sub(r"[^a-z0-9]+", "-", m.group(3).lower()).strip("-")[:48].strip("-")
    return d, key, slug


def pdir(n):
    hits = sorted((ROOT / ".process").glob(f"{n}-*"))
    return hits[0] if hits else None


def start(n):
    d, key, slug = story(n)
    if run("git", "status", "--porcelain").stdout.strip():
        run("git", "stash", "push", "-u", "-m", f"autopilot-leftover-before-{n}")
        print(f"STASHED leftover changes as autopilot-leftover-before-{n}")
    run("git", "switch", "main"); run("git", "pull", "--ff-only")
    branch = f"feature/{n}-{slug}"
    if run("git", "rev-parse", "--verify", branch, check=False).returncode == 0:
        run("git", "switch", branch)
    else:
        run("git", "switch", "-c", branch)
    p = ROOT / ".process" / f"{n}-{slug}"; p.mkdir(parents=True, exist_ok=True)
    (p / "00-story.md").write_text(f"# {d['title']}\n\nIssue: #{n}\n\n{d['body']}\n", encoding="utf-8")
    if not (p / "00-acceptance.md").exists():
        (p / "00-acceptance.md").write_text(f"# Acceptance — {d['title']}\n\n- Date: {now()}\n- Story: #{n} ({key})\n- Mode: autopilot\n- {AUTOPILOT}\n- Auto-accepted under autopilot.\n", encoding="utf-8")
    if not (p / "04-metrics.md").exists():
        (p / "04-metrics.md").write_text(f"# Metrics — {d['title']}\n\nBranch: `{branch}` (base `main`)\n\n| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |\n|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|\n", encoding="utf-8")
    print(f"DIR={p.relative_to(ROOT).as_posix()}\nBRANCH={branch}\nKEY={key}\nSTATE={d['state']}")


def verify(n):
    results = []
    if (ROOT / "api").exists() and list((ROOT / "api").glob("*.sln*")):
        b = run("dotnet", "build", "api/", "-nologo", "-v", "q", check=False)
        t = run("dotnet", "test", "api/", "-nologo", "-v", "q", check=False) if b.returncode == 0 else b
        results.append(("dotnet", b.returncode == 0 and t.returncode == 0, (t.stdout + t.stderr)[-1500:]))
    if (ROOT / "web" / "package.json").exists():
        b = run("npm", "--prefix", "web", "run", "build", check=False)
        t = run("npm", "--prefix", "web", "test", "--", "--run", check=False) if b.returncode == 0 else b
        results.append(("web", b.returncode == 0 and t.returncode == 0, (t.stdout + t.stderr)[-1500:]))
    if (ROOT / "ai").exists() and list((ROOT / "ai").rglob("test_*.py")):
        py = str(ROOT / "ai" / ".venv" / "Scripts" / "python.exe") if (ROOT / "ai" / ".venv").exists() else "python"
        t = run(py, "-m", "pytest", "-q", check=False, cwd=ROOT / "ai")
        results.append(("ai", t.returncode == 0, (t.stdout + t.stderr)[-1500:]))
    ok = all(r[1] for r in results)
    for name, good, tail in results:
        print(f"{name}: {'PASS' if good else 'FAIL'}")
        if not good:
            print(tail)
    print("VERIFY=" + ("PASS" if ok else "FAIL") + ("" if results else " (no stacks yet)"))


def commit(msg):
    run("git", "add", "-A")
    if run("git", "diff", "--cached", "--quiet", check=False).returncode != 0:
        run("git", "commit", "-q", "-m", msg + "\n\n" + TRAILER)


def pr(n):
    d, key, slug = story(n)
    p = pdir(n)
    commit(f"feat({key}): {d['title'].split('] ',1)[1]}")
    branch = run("git", "branch", "--show-current").stdout.strip()
    run("git", "push", "-q", "-u", "origin", branch)
    if not HAS_GH:
        existing = []
    else:
        existing = json.loads(run("gh", "pr", "list", "--repo", REPO, "--head", branch, "--json", "number").stdout)
    if existing:
        print(f"PR={existing[0]['number']}"); return
    plan = (p / "01-plan.md").read_text(encoding="utf-8") if (p / "01-plan.md").exists() else ""
    goal = re.search(r"## Goal\s*\n(.*?)(\n## |\Z)", plan, re.S)
    rel = p.relative_to(ROOT).as_posix()
    body = (f"{goal.group(1).strip() if goal else d['title']}\n\nCloses #{n}\n\n**Pipeline artifacts** (`{rel}/`): "
            + " · ".join(f"[{f.name}](../blob/{branch}/{rel}/{f.name})" for f in sorted(p.glob('*.md')))
            + "\n\n🤖 Generated with [Claude Code](https://claude.com/claude-code)")
    if not HAS_GH:
        f = pathlib.Path(tempfile.gettempdir()) / f"pr-body-{n}.md"; f.write_text(body, encoding="utf-8")
        print(f"PR=OPEN_WITH_TOOLS BRANCH={branch} TITLE={d['title']} BODY_FILE={f}"); return
    url = run("gh", "pr", "create", "--repo", REPO, "--base", "main", "--head", branch, "--title", d["title"], "--body", body).stdout.strip()
    print(f"PR={url.rsplit('/',1)[1]}")


def poll(n, prn, timeout=900):
    p = pdir(n)
    start_t = time.time(); seen_bot = False; skipped = None
    def bot(u): return "coderabbit" in (u or "").lower()
    while time.time() - start_t < timeout:
        ic = json.loads(run("gh", "api", f"repos/{REPO}/issues/{prn}/comments", check=False).stdout or "[]")
        rv = json.loads(run("gh", "api", f"repos/{REPO}/pulls/{prn}/reviews", check=False).stdout or "[]")
        rc = json.loads(run("gh", "api", f"repos/{REPO}/pulls/{prn}/comments", check=False).stdout or "[]")
        botc = [c for c in ic if bot(c["user"]["login"])]
        botr = [r for r in rv if bot(r["user"]["login"])]
        botrc = [c for c in rc if bot(c["user"]["login"])]
        if botc or botr or botrc:
            seen_bot = True
        for c in botc:
            b = c.get("body") or ""
            lim = re.search(r"Next included review available in (\d+) minute", b)
            if lim and not botr and not getattr(poll, "retried", False):
                poll.retried = True
                wait = (int(lim.group(1)) + 1) * 60
                print(f"RATE_LIMIT wait {wait}s then re-request", flush=True)
                time.sleep(wait)
                run("gh", "pr", "comment", str(prn), "--repo", REPO, "--body", "@coderabbitai review")
                start_t = time.time(); skipped = None
                break
            if getattr(poll, "retried", False) and "Next included review available" in b and c.get("id") == getattr(poll, "limit_id", c.get("id")):
                pass
            if ("Review skipped" in b) and not botr:
                m = re.search(r"## Review skipped\s*>\s*\n>\s*(.*?)\n", b, re.S)
                skipped = (m.group(1).strip() if m else "skipped / rate limited")
        if botr or botrc:
            time.sleep(60)
            rc = json.loads(run("gh", "api", f"repos/{REPO}/pulls/{prn}/comments").stdout or "[]")
            rv = json.loads(run("gh", "api", f"repos/{REPO}/pulls/{prn}/reviews").stdout or "[]")
            items = [c for c in rc if bot(c["user"]["login"])]
            out = [f"# CodeRabbit comments — PR #{prn}\n\nCollected {now()}. Verbatim.\n"]
            for i, c in enumerate(items, 1):
                out.append(f"## RC{i} — `{c.get('path')}:{c.get('line') or c.get('original_line')}`\n\n{c['body']}\n")
            for i, r in enumerate([r for r in rv if bot(r['user']['login']) and (r.get('body') or '').strip()], 1):
                out.append(f"## PC{i} — review body\n\n{r['body']}\n")
            (p / "05-coderabbit-comments.md").write_text("\n".join(out), encoding="utf-8")
            print(f"ACTIONABLE={len(items)}"); return
        if skipped:
            (p / "05-coderabbit-comments.md").write_text(f"# CodeRabbit comments — PR #{prn}\n\nNo review: CodeRabbit posted \"{skipped}\" ({now()}). Nothing to triage.\n", encoding="utf-8")
            print(f"ACTIONABLE=0 SKIPPED={skipped}"); return
        time.sleep(30)
    (p / "05-coderabbit-comments.md").write_text(f"# CodeRabbit comments — PR #{prn}\n\nNo actionable CodeRabbit comments within 15 minutes ({now()}). Bot activity seen: {seen_bot}.\n", encoding="utf-8")
    print(f"ACTIONABLE=0 TIMEOUT BOT_SEEN={seen_bot}")


def push(n, msg):
    commit(msg)
    run("git", "push", "-q")
    print("PUSHED")


def merge(n, prn):
    commit(f"chore(#{n}): pipeline artifacts")
    if run("git", "status", "-sb").stdout.find("ahead") >= 0:
        run("git", "push", "-q")
    if not HAS_GH:
        print("PUSHED_ARTIFACTS — wait for CI, merge, then run: pipeline_orch.py sync"); return
    time.sleep(15)
    c = run("gh", "pr", "checks", str(prn), "--repo", REPO, "--watch", "--interval", "20", check=False)
    if c.returncode != 0 and "no checks reported" not in (c.stdout + c.stderr):
        print("CHECKS=FAILED"); print((c.stdout + c.stderr)[-1500:]); return
    r = None
    for attempt in range(3):
        r = run("gh", "pr", "merge", str(prn), "--repo", REPO, "--squash", "--delete-branch", check=False)
        if r.returncode == 0:
            break
        time.sleep(20)
    if r.returncode != 0:
        print("MERGE=FAILED\n" + r.stderr[-800:]); return
    run("git", "switch", "main"); run("git", "pull", "--ff-only")
    st = json.loads(run("gh", "issue", "view", str(n), "--repo", REPO, "--json", "state").stdout)["state"]
    if st != "CLOSED":
        run("gh", "issue", "close", str(n), "--repo", REPO, "--comment", f"Implemented in #{prn}.")
    print(f"MERGE=OK main={run('git','rev-parse','--short','HEAD').stdout.strip()}")


def issue(label, title, body_file, n=None):
    run("gh", "label", "create", label, "--repo", REPO, "--force")
    body = pathlib.Path(body_file).read_text(encoding="utf-8")
    if n:
        body += f"\n\nRelated story: #{n}"
    body += "\n\n_Opened by the autopilot run._"
    url = run("gh", "issue", "create", "--repo", REPO, "--title", title, "--label", label, "--body", body).stdout.strip()
    print(url)


def sync():
    run("git", "switch", "main"); run("git", "pull", "--ff-only")
    print(f"MAIN={run('git','rev-parse','--short','HEAD').stdout.strip()}")


def metric(n, row):
    """row: '#|Stage|Agent|Model|duration_ms|tokens|tool_uses|outcome' — finish = now, start = now - duration."""
    p = pdir(n)
    c = [x.strip() for x in row.split("|")]
    if len(c) == 8 and c[4].isdigit():
        ms = int(c[4]); end = datetime.datetime.now(); start_ = end - datetime.timedelta(milliseconds=ms)
        m, s = divmod(ms // 1000, 60)
        c = c[:4] + [start_.strftime("%H:%M"), end.strftime("%H:%M"), f"{m}m {s}s", f"{int(c[5]):,}" if c[5].isdigit() else c[5], c[6], c[7]]
    with open(p / "04-metrics.md", "a", encoding="utf-8") as f:
        f.write("| " + " | ".join(c) + " |\n")
    print("OK")


if __name__ == "__main__":
    cmd, *a = sys.argv[1:]
    {"start": start, "verify": verify, "pr": pr, "poll": poll, "push": push, "merge": merge, "issue": issue, "metric": metric, "sync": sync}[cmd](*a)
