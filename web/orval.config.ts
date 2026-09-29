import { defineConfig } from 'orval';

export default defineConfig({
  api: {
    input: { target: '../api/openapi/v1.json' },
    output: {
      mode: 'tags-split',
      target: 'src/shared/api/generated',
      schemas: 'src/shared/api/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        mutator: { path: 'src/shared/lib/http.ts', name: 'http' },
        fetch: { includeHttpResponseReturnType: false },
        query: { useSuspenseQuery: true, signal: true },
        mock: { exactOptional: true },
      },
      headers: true,
      mock: { generators: [{ type: 'msw' }] },
    },
  },
  apiZod: {
    input: { target: '../api/openapi/v1.json' },
    output: {
      client: 'zod',
      mode: 'tags-split',
      target: 'src/shared/api/generated/zod',
      fileExtension: '.zod.ts',
      override: {
        operations: {
          GetAuditLogs: { zod: { generate: { query: false } } },
          GetQuestions: { zod: { generate: { query: false } } },
          GetValidationQueue: { zod: { generate: { query: false } } },
          GetSessionHistory: { zod: { generate: { query: false } } },
          GetMyPayments: { zod: { generate: { query: false } } },
          GetPaymentLog: { zod: { generate: { query: false } } },
        },
      },
    },
  },
});
