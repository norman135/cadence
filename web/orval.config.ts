import { defineConfig } from 'orval';

// Generates a typed API client and TanStack Query hooks from the OpenAPI document the backend
// produces at build time. Regenerate with `npm run api:generate` after changing the API;
// CI fails if the committed client is out of date.
export default defineConfig({
  cadence: {
    input: {
      target: '../openapi/cadence.json',
    },
    output: {
      mode: 'tags-split',
      target: 'src/shared/api/generated/endpoints',
      schemas: 'src/shared/api/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        // Every request goes through one fetch wrapper that handles errors (see http-client.ts).
        mutator: {
          path: 'src/shared/api/http-client.ts',
          name: 'httpClient',
        },
        // Hooks return the response body directly; failures surface as ApiError.
        fetch: {
          includeHttpResponseReturnType: false,
        },
      },
    },
  },
});
