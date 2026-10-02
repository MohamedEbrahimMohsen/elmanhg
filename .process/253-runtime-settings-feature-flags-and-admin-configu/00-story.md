# [E18.S1] Runtime settings, feature flags and admin Configuration page

Issue: #253

Epic: #252

As an admin I have one Configuration page that shows everything configurable in the system. I can change business settings and feature flags at runtime. I can also see, read-only, which providers are real or fake and whether each secret is set.

Dev decision (2026-10-01): "I need to have a configuration page where it has everything that is configurable, the feature flags, ...etc."

**Design rules**
- **Editable at runtime:** business settings and feature flags.
  - They are stored in the database, audited and cached, and take effect without a restart.
  - Every setting has a typed definition: key, group, type, default, min/max or allowed values, and a description in ar and en.
  - The default comes from the existing appsettings value, so behaviour stays the same until an admin overrides it.
  - "Reset to default" removes the override.
- **Read-only on the page:** infrastructure configuration. This covers the environment, the provider for each integration (fake or real), model names and the storage kind.
- **Secrets are never returned by the API.** The page shows only "set" or "not set".
- **Safety switches are not editable from the UI.** These are switches that could give money away or bypass security, for example allowing fake payments. The page shows them read-only.
- **Every change is written to the audit log:** who made it, the old value and the new value.

### Sub-tasks
- [ ] Setting registry and typed runtime-settings store: a database table and migration, a cache with invalidation, and a typed accessor for handlers
- [ ] Admin API:
  - list settings by group, with the current value, the default and whether it is overridden;
  - update one setting, with validation;
  - reset a setting to its default;
  - a read-only section for infrastructure and secret status.
- [ ] Seed the registry with the existing business options that are safe to change at runtime: Ask a Teacher SLA and reminder hours, upload limits, free-tier limits, grading thresholds, and similar
- [ ] Admin Configuration page in ar/en (RTL): grouped sections, an editor for each setting type, validation errors, reset, and the read-only infrastructure section
- [ ] Docs: the PRD admin section and docs/implementation-report.md §4 point to the page

