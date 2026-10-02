# [E18.S5] OpenAI-compatible LLM provider replacing Anthropic

Issue: #257

Epic: #252

As the owner I want the AI features to run on a cheap model that is still good, and never on the Anthropic API. The AI features are the avatar tutor, essay grading and math step grading.

Dev decision (2026-10-01): "I will never use Anthropic API Key, replace it with any cheap with good quality model, cheaper is better."

**Rules**
- **One adapter:** replace the Anthropic adapter with a single OpenAI-compatible Chat Completions adapter.
  - It is configured by base URL, API key and model, so the same code works with OpenAI, Google Gemini (through its OpenAI-compatible endpoint) and DeepSeek.
  - Remove the `anthropic` dependency and its settings.
- **Default model and pricing:**
  - The default real model is OpenAI's budget tier, because an OpenAI key is already needed for embeddings and Whisper.
  - The model for each pipeline (chat, essay grading, math step grading) stays configurable, and so do the per-token prices.
- **Structured output:** use the JSON-schema response format where the provider supports it. Otherwise, fall back to JSON that is validated after the reply.
- **Lesson citations:**
  - Each source is passed with a reference id, and the model cites the ids inline.
  - The citations are parsed, and only ids that were supplied are accepted, so the model can't invent sources.
- **Kept as they are:** fakes stay the default, and the prompt-injection protections in the existing pipelines remain.

### Sub-tasks
- [ ] OpenAI-compatible model client to replace the Anthropic client: timeouts, retries, usage and cost metering, and error mapping
- [ ] The citation protocol and its parsing for the avatar chat, and structured output for the two grading pipelines
- [ ] Settings, env examples, the deploy compose file and docs: the constitution stack, the PRD AI section and implementation report §4
- [ ] Unit tests with recorded fixtures, and the eval harness pointed at the new client

