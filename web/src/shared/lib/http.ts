import { z } from 'zod';
import { env } from '@/app/env';
import { ApiError, networkErrorCode, unhandledErrorCode } from './apiError';

const errorBodySchema = z.object({ code: z.string().optional(), message: z.string().optional() });

export function resolveApiUrl(path: string): string {
  return new URL(path, env.VITE_API_BASE_URL === '' ? window.location.origin : env.VITE_API_BASE_URL).toString();
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const parsed = errorBodySchema.safeParse(await response.json());
    const codes = parsed.success
      ? (parsed.data.code ?? '')
          .split(',')
          .map((code) => code.trim())
          .filter((code) => code !== '')
      : [];
    return new ApiError(response.status, codes.length > 0 ? codes : [unhandledErrorCode], parsed.data?.message ?? '');
  } catch {
    return new ApiError(response.status, [unhandledErrorCode], '');
  }
}

export async function http<T>(url: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  const language = document.documentElement.lang;
  if (language !== '') {
    headers.set('Accept-Language', language);
  }

  let response: Response;
  try {
    response = await fetch(resolveApiUrl(url), { ...init, headers, credentials: 'include' });
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      throw error;
    }
    throw new ApiError(0, [networkErrorCode], error instanceof Error ? error.message : '');
  }

  if (!response.ok) {
    throw await toApiError(response);
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}
