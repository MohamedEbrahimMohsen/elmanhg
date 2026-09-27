---
name: momenta-dependency-policy
description: Use before adding, upgrading or removing any third-party package (NuGet, npm/pnpm, PyPI/uv, pub.dev, Maven/Gradle) in any stack, and when reviewing a diff that touches a manifest or lockfile — when a dependency may be added, how to verify it exists, release-age, licence and lockfile rules.
---

# Momenta dependency policy

LLMs invent package names (about 1 in 5 suggestions across open models, about 1 in 20 for frontier models; many invented names repeat and get registered by attackers). Every rule below is mechanical so a reviewer can say yes/no.

## 1. When a dependency may be added

- Only when the approved plan (`02-plan.md`) names it: exact package id, exact version, ecosystem, and the reason the standard library / existing dependencies cannot do it.
- The implementer never adds a package the plan does not name. Need one mid-task → stop and write `BLOCKED: needs dependency <name>@<version> — <why>`.
- Prefer, in order: platform/standard library → a dependency already in the lockfile → a new dependency.
- One library per concern per repo (one HTTP client, one validation lib, one test framework, one ORM).
- Upgrades follow the same rule: the plan names from→to version; major upgrades list breaking changes read from the changelog.
- Removals: allowed when the plan names them; lockfile regenerated in the same change.

## 2. Verify it exists (never guess a name)

Run the command, paste its output in the implementation report. A package whose name you have not seen in registry output in this session is not added.

| Ecosystem | Exists + versions | Metadata (licence, repo, dates) |
|---|---|---|
| npm / pnpm | `npm view <pkg> versions --json` | `npm view <pkg>@<ver> name version license repository.url time --json` · downloads: `curl -s https://api.npmjs.org/downloads/point/last-week/<pkg>` |
| NuGet | `dotnet package search <Id> --exact-match` | `https://www.nuget.org/packages/<Id>/<ver>` (licence, published date, repo) · versions: `curl -s https://api.nuget.org/v3-flatcontainer/<id-lowercase>/index.json` |
| PyPI (uv/pip) | `pip index versions <pkg>` | `curl -s https://pypi.org/pypi/<pkg>/<ver>/json \| jq '.info.license, .info.license_expression, .info.project_urls, .urls[0].upload_time_iso_8601'` |
| pub.dev (Dart/Flutter) | `curl -s https://pub.dev/api/packages/<pkg> \| jq '.latest.version'` | `https://pub.dev/packages/<pkg>/score` (publisher, likes, pub points) |
| Maven Central (Kotlin/Android) | `curl -s "https://search.maven.org/solrsearch/select?q=g:<group>+AND+a:<artifact>&core=gav&rows=10&wt=json"` | POM `<licenses>` on `https://repo1.maven.org/maven2/<group-path>/<artifact>/<ver>/` |

Checks (each yes/no, recorded in the report):
- [ ] Name matches the registry exactly (case, scope, hyphen vs underscore); not a typo-distance neighbour of a popular package.
- [ ] Publisher/owner is the expected project (repo link resolves to the real source; npm scope / NuGet owner / PyPI maintainer matches).
- [ ] Package first published ≥ 30 days ago.
- [ ] Chosen version published ≥ 7 days ago (§3).
- [ ] Popularity floor: npm ≥ 1,000 weekly downloads; NuGet ≥ 100,000 total downloads; PyPI/pub.dev/Maven: active repo with a release in the last 12 months. Below the floor → human approval.
- [ ] Not deprecated / not archived (`npm view <pkg> deprecated`, `dotnet list package --deprecated` after adding, repo archived banner).
- [ ] No install scripts, or install scripts reviewed and allow-listed (npm: `npm view <pkg>@<ver> scripts`).
- [ ] Licence on the allow-list (§4).
- [ ] No known vulnerability in the chosen version (§6 commands).

Any unchecked box → do not add; `BLOCKED: <box> for <pkg>`.

## 3. Release-age rule

- Default: the chosen version must be ≥ 7 days old. Security-fix versions may be < 7 days old only when the plan cites the advisory id and a human approves.
- Enforce in tooling where it exists:

| Tool | Setting |
|---|---|
| pnpm 11 | `minimumReleaseAge: 10080` (minutes) in `pnpm-workspace.yaml`; default is 1440 |
| npm ≥ 11.10 | `min-release-age=7` (days) in `.npmrc` (verify with `npm help config`) |
| Yarn ≥ 4.10 | `npmMinimalAgeGate: "7d"` in `.yarnrc.yml` |
| uv | `exclude-newer` in `[tool.uv]` (timestamp; check relative-duration support with `uv help lock`) |
| NuGet, pub, Gradle | no native gate: check the publish date manually (§2) |

## 4. Licences

- Allowed without approval: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, 0BSD, Unlicense, PSF-2.0, Zlib.
- Allowed with reviewer note: MPL-2.0, EPL-2.0 (file-level copyleft; no modification of the library's files).
- Human approval + ADR required: LGPL-*, any dual/commercial licence (RPL-1.5 + commercial, "community edition" tiers, licence-key packages), BUSL, Elastic, SSPL, Commons Clause, PolyForm, "source-available".
- Forbidden in shipped code: GPL-*, AGPL-*, no licence / "All rights reserved", unknown.
- Known commercial switches (2025–26), do not add without approval: FluentAssertions ≥ 8, MediatR ≥ 13, AutoMapper ≥ 15, MassTransit ≥ 9. Check changelogs for licence changes on every major upgrade.
- Licence listing commands: `pnpm licenses list --prod` · `uvx pip-licenses --from=mixed` · .NET: the package page licence field (or the `nuget-license` tool if the repo already uses it) · Gradle: the repo's licence plugin if configured.
- CI gate: `actions/dependency-review-action` with `allow-licenses` = the allowed list and `fail-on-severity: high`.

## 5. Lockfiles and pinning

| Ecosystem | Lockfile (committed) | CI install (fails on drift) | Pinning rule |
|---|---|---|---|
| pnpm | `pnpm-lock.yaml` | `pnpm install --frozen-lockfile` | direct deps exact (`save-exact=true` / `pnpm add -E`) |
| npm | `package-lock.json` | `npm ci` | `save-exact=true` in `.npmrc` |
| NuGet | `packages.lock.json` per project (`RestorePackagesWithLockFile=true`) | `dotnet restore --locked-mode` | exact versions in `Directory.Packages.props` (CPM) |
| uv | `uv.lock` | `uv sync --locked` | ranges in `pyproject.toml` allowed; the lock is the pin; LLM SDKs pinned with `==` |
| pip (legacy) | `requirements.txt` with hashes | `pip install --require-hashes -r requirements.txt` | `==` + `--hash` |
| pub | `pubspec.lock` (apps) | `flutter pub get --enforce-lockfile` | caret ranges + lock |
| Gradle | `gradle.lockfile` + `gradle/verification-metadata.xml` | `--locked` builds via `dependencyLocking { lockAllConfigurations() }` | version catalog `libs.versions.toml` |

- Lockfile changes only come from the package manager, never hand edits.
- A manifest change without the matching lockfile change (or the reverse) is a blocking finding.
- Lockfile diff reviewed: every newly added transitive package with an install script or a < 30-day-old first publish is flagged.
- Docker images pinned to a version tag (and digest for production); never `:latest`. GitHub Actions pinned to a full commit SHA.

## 6. Install hygiene and scanning

- npm/pnpm: `ignore-scripts=true` (npm) / pnpm `allowBuilds` allow-list for packages that need postinstall.
- Registries: one source per scope. npm scoped registries (`@org:registry=`), NuGet `packageSourceMapping`, pip `--index-url` only (never `--extra-index-url` mixing private names), Gradle `exclusiveContent`.
- Vulnerability scan on every PR touching a manifest/lockfile:
  - `.NET`: `dotnet list package --vulnerable --include-transitive` (NuGetAudit is on by default for net10.0)
  - Node: `pnpm audit --prod` / `npm audit --omit=dev --audit-level=high`; `npm audit signatures`
  - Python: `uvx pip-audit` (or `uv export --frozen --no-hashes -o /tmp/req.txt && pip-audit -r /tmp/req.txt`)
  - Any: `osv-scanner scan source -r .`
- New High/Critical in a runtime dependency → blocking. Suppressions must name the advisory id, a reason, and an expiry date (`osv-scanner.toml` `ignoreUntil`, `NuGetAuditSuppress`, `pip-audit --ignore-vuln`); the security reviewer flags every new suppression for human confirmation.

## 7. Report block (paste into `03-implementation-<stack>.md`)

```markdown
### Dependencies (example row — replace with real command output)
| Package | Version | Ecosystem | Plan ref | Exists (cmd) | First published | Version age | Licence | Downloads | Vulns |
|---|---|---|---|---|---|---|---|---|---|
| zod | 4.2.1 | npm | 02-plan §Deps | `npm view zod@4.2.1 version` → 4.2.1 | 2020-03-07 | 21 d | MIT | 40M/wk | none |
Lockfile updated: pnpm-lock.yaml (+3 packages)
```

## 8. Never

- Guess or "remember" a package name without registry output in this session.
- Add a package the plan does not name, or a different version than the plan names.
- Install with `--force`, `--legacy-peer-deps`, `--no-verify`, `--skip-lock`, or unlocked installs in CI.
- Copy a snippet that imports a package not in the lockfile without running §2.
- Vendor third-party code into the repo to avoid this policy.
- Add a package with a licence outside §4 without the ADR.
