import { z } from 'zod';

const envSchema = z.object({
  VITE_API_BASE_URL: z.union([z.literal(''), z.url()]).default(''),
});

export type Env = z.infer<typeof envSchema>;

export function parseEnv(source: Record<string, unknown>): Env {
  return envSchema.parse(source);
}

export const env = parseEnv(import.meta.env);
