"""Import docs/backlog.json into a GitHub repo and a GitHub Project (v2).

Usage:
    python scripts/import_backlog.py <owner/repo> <project-owner> <project-number> [--dry-run]

Requires `gh` logged in as an account with write access to the repo and the
project, with token scopes: repo, project.

Creates: labels, one issue per epic, one issue per story (sub-tasks as a
checklist inside the story), links stories to epics as GitHub sub-issues,
and adds every issue to the project.

Safe to rerun: issues are matched by exact title, project items by URL,
and sub-issue links by number. Nothing is created twice.
"""
import json, subprocess, sys, pathlib


def gh(*args):
    r = subprocess.run(["gh", *args], capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"gh {' '.join(args)}\n{r.stderr}")
    return r.stdout.strip()


def gh_json(*args):
    return json.loads(gh(*args) or "null")


def main():
    if len(sys.argv) < 4:
        raise SystemExit(__doc__)
    repo, powner, pnum = sys.argv[1:4]
    dry = "--dry-run" in sys.argv
    root = pathlib.Path(__file__).resolve().parent.parent
    data = json.loads((root / "docs" / "backlog.json").read_text(encoding="utf-8"))

    counts = {"epics": len(data["epics"]),
              "stories": sum(len(e["stories"]) for e in data["epics"]),
              "tasks": sum(len(s["tasks"]) for e in data["epics"] for s in e["stories"])}
    print("Backlog:", counts)
    if dry:
        return

    print("== Labels")
    for l in data["labels"]:
        gh("label", "create", l, "--repo", repo, "--force")
        print("  ", l)

    # Existing state, so reruns never duplicate.
    existing = {}
    for it in gh_json("issue", "list", "--repo", repo, "--limit", "500", "--state", "open",
                      "--json", "number,title"):
        existing.setdefault(it["title"], it["number"])
    in_project = {it["content"]["url"]
                  for it in gh_json("project", "item-list", pnum, "--owner", powner,
                                    "--limit", "500", "--format", "json")["items"]
                  if it.get("content", {}).get("url")}
    print(f"Existing open issues: {len(existing)}, project items: {len(in_project)}")

    def db_id(num):
        return gh("api", f"repos/{repo}/issues/{num}", "--jq", ".id")

    def ensure_issue(title, labels, body):
        if title in existing:
            num = existing[title]
            url = f"https://github.com/{repo}/issues/{num}"
            created = False
        else:
            url = gh("issue", "create", "--repo", repo, "--title", title,
                     "--label", ",".join(labels), "--body", body)
            num = int(url.rsplit("/", 1)[1])
            existing[title] = num
            created = True
        if url not in in_project:
            gh("project", "item-add", pnum, "--owner", powner, "--url", url)
            in_project.add(url)
        return num, created

    print("== Epics and stories")
    for e in data["epics"]:
        body = e["description"] + "\n\n### Stories\n" + "\n".join(f"- [ ] {s['title']}" for s in e["stories"])
        enum, created = ensure_issue(f"[{e['key']}] {e['title']}", ["epic", *e["labels"]], body)
        print(f"  {e['key']} -> #{enum}", "" if created else "(exists)")
        linked = {it["number"] for it in gh_json("api", f"repos/{repo}/issues/{enum}/sub_issues")}
        for i, s in enumerate(e["stories"], 1):
            sbody = (s["description"] + f"\n\nEpic: #{enum}\n\n### Sub-tasks\n"
                     + "\n".join(f"- [ ] {t}" for t in s["tasks"]))
            snum, created = ensure_issue(f"[{e['key']}.S{i}] {s['title']}", ["story", *e["labels"]], sbody)
            if snum not in linked:
                try:
                    gh("api", "-X", "POST", f"repos/{repo}/issues/{enum}/sub_issues",
                       "-F", f"sub_issue_id={db_id(snum)}")
                except SystemExit as ex:
                    print("    (sub-issue link failed, continuing)", str(ex).splitlines()[-1])
            print(f"    {e['key']}.S{i} -> #{snum}", "" if created else "(exists)")
    print("Done.")


if __name__ == "__main__":
    main()
