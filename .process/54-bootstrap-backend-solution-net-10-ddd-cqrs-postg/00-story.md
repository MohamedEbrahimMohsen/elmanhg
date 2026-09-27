# [E1.S1] Bootstrap backend solution (.NET 10, DDD/CQRS, PostgreSQL)

Issue: #54

As a developer I need a runnable API skeleton so that every later story has a home. PRD §18.

Epic: #53

### Sub-tasks
- [ ] Create solution from the DDD template: Api, Application, Domain, Infrastructure, Tests
- [ ] Add PostgreSQL via EF Core 10 with Npgsql, JSONB support and migrations pipeline
- [ ] Configure MediatR pipeline: validation, logging, unit-of-work behaviours
- [ ] Add health endpoint, Swagger, structured logging, and environment config
- [ ] Add CI workflow: restore, build, test, vulnerability scan
