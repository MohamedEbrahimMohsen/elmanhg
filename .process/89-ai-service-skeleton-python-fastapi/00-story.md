# [E8.S1] AI service skeleton (Python FastAPI)

Issue: #89

As a developer I have a separate AI service the .NET API calls so that prompts and models iterate independently. PRD §18.

Epic: #88

### Sub-tasks
- [ ] FastAPI project with auth between services, health endpoint, structured logging
- [ ] Claude API client with model and prompt version configuration
- [ ] Deployment config and CI
- [ ] Contract: chat endpoint accepting context bundle and message history
