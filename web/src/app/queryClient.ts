import { QueryClient } from '@tanstack/react-query';

const defaultStaleTimeMs = 30_000;

export function createQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { staleTime: defaultStaleTimeMs, retry: 1 } } });
}
