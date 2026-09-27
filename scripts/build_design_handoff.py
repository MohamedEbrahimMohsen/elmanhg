"""Build the Claude Design handoff zip from the repo's source files.

Usage:
    python scripts/build_design_handoff.py

Writes elmanhg-claude-design-handoff.zip at the repo root (git-ignored).
Upload the zip to Claude Design and paste docs/claude-design-prompt.md as the message.

Zip layout matches the paths the prompt refers to:
    PROMPT.md                  <- docs/claude-design-prompt.md
    docs/design-system.md      <- docs/design-system.md
    docs/PRD.md                <- docs/PRD.md
    prototype/README.md        <- docs/prototype.md
    prototype/index.html, app.js, data.js, styles.css
"""
import pathlib, zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "elmanhg-claude-design-handoff.zip"

FILES = {
    "PROMPT.md": "docs/claude-design-prompt.md",
    "docs/design-system.md": "docs/design-system.md",
    "docs/PRD.md": "docs/PRD.md",
    "prototype/README.md": "docs/prototype.md",
    "prototype/index.html": "prototype/index.html",
    "prototype/app.js": "prototype/app.js",
    "prototype/data.js": "prototype/data.js",
    "prototype/styles.css": "prototype/styles.css",
}


def main():
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
        for arc, src in FILES.items():
            z.write(ROOT / src, arc)
    print(f"{OUT.name}: {len(FILES)} files, {OUT.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
