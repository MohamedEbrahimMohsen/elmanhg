# [E18.S6] Email alert receiver

Issue: #258

Epic: #252

As the owner I receive production alerts by email.

Dev decisions on #213:
- Alerts: "Emails only for now."
- Licensing: no objection raised to Grafana, Loki and Tempo being AGPL. They run unmodified as separate containers.

### Sub-tasks
- [ ] An Alertmanager email receiver over SMTP (Resend SMTP by default)
  - It is configured from host env or secret files, with placeholder values.
  - The null receiver stays the default when it isn't configured.
- [ ] Route all current alert rules to the email receiver, with sensible grouping and repeat intervals
- [ ] docs/observability.md and docs/deployment.md: the variables, and how to send a test alert

