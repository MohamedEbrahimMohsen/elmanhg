import { z } from 'zod';
import { roles } from '@/features/session';

const envSchema = z.object({
  VITE_API_BASE_URL: z.union([z.literal(''), z.url()]).default(''),
  VITE_DEV_SESSION_ROLE: z.preprocess((value) => (value === '' ? undefined : value), z.enum(roles).optional()),
});

export type Env = z.infer<typeof envSchema>;

export function parseEnv(source: Record<string, unknown>): Env {
  return envSchema.parse(source);
}

export const env = parseEnv(import.meta.env);
