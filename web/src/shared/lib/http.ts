import { z } from 'zod';
import { env } from '@/app/env';
import { ApiError, networkErrorCode, unhandledErrorCode } from './apiError';
import { getAccessToken, notifyExpired, refreshOnce } from './authToken';

const errorBodySchema = z.object({ code: z.string().optional(), message: z.string().optional() });

const refreshPath = '/api/auth/refresh';

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

async function send(url: string, init: RequestInit): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  const language = document.documentElement.lang;
  if (language !== '') {
    headers.set('Accept-Language', language);
  }
  const token = getAccessToken();
  if (token !== null) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  try {
    return await fetch(resolveApiUrl(url), { ...init, headers, credentials: 'include' });
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      throw error;
    }
    throw new ApiError(0, [networkErrorCode], error instanceof Error ? error.message : '');
  }
}

async function parse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw await toApiError(response);
  }
  const contentType = response.headers.get('Content-Type') ?? '';
  if (contentType !== '' && !contentType.includes('json')) {
    return (await response.blob()) as T;
  }
  const body = await response.text();
  if (body === '') {
    return undefined as T;
  }
  return JSON.parse(body) as T;
}

export async function http<T>(url: string, init: RequestInit = {}): Promise<T> {
  let response = await send(url, init);

  if (response.status === 401 && !url.startsWith(refreshPath)) {
    if (await refreshOnce()) {
      response = await send(url, init);
      if (response.status === 401) {
        notifyExpired();
      }
    } else {
      notifyExpired();
    }
  }

  return parse<T>(response);
}
