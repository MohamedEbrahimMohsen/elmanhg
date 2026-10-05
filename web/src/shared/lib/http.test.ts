import { http as mswHttp, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { i18n } from '@/app/i18n';
import { server } from '@/test/msw/server';
import { ApiError } from './apiError';
import { registerAuthHandlers, setAccessToken } from './authToken';
import { http } from './http';

describe('http', () => {
  it('returns the parsed body on success', async () => {
    server.use(mswHttp.get('*/api/probe', () => HttpResponse.json({ value: 1 })));

    await expect(http('/api/probe')).resolves.toEqual({ value: 1 });
  });

  it('throws ApiError with status and every code from the error body', async () => {
    server.use(
      mswHttp.get('*/api/probe', () =>
        HttpResponse.json({ code: 'VALIDATION_FAILED,OTHER', message: 'm' }, { status: 422 }),
      ),
    );

    const error: unknown = await http('/api/probe').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 422, code: 'VALIDATION_FAILED', codes: ['VALIDATION_FAILED', 'OTHER'] });
  });

  it('throws UNHANDLED_EXCEPTION when the error body is not JSON', async () => {
    server.use(mswHttp.get('*/api/probe', () => HttpResponse.text('boom', { status: 500 })));

    const error: unknown = await http('/api/probe').catch((caught: unknown) => caught);

    expect(error).toMatchObject({ status: 500, code: 'UNHANDLED_EXCEPTION' });
  });

  it('throws NETWORK_ERROR when the request fails', async () => {
    server.use(mswHttp.get('*/api/probe', () => HttpResponse.error()));

    const error: unknown = await http('/api/probe').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 0, code: 'NETWORK_ERROR' });
  });

  it('sends Accept-Language from the document language', async () => {
    await i18n.changeLanguage('ar');
    server.use(mswHttp.get('*/api/probe', ({ request }) => HttpResponse.json(request.headers.get('accept-language'))));

    await expect(http('/api/probe')).resolves.toBe('ar');
  });

  it('returns undefined for 204', async () => {
    server.use(mswHttp.get('*/api/probe', () => new HttpResponse(null, { status: 204 })));

    await expect(http('/api/probe')).resolves.toBeUndefined();
  });

  it('returns a Blob for a non-JSON response', async () => {
    const bytes = new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0x14]);
    server.use(
      mswHttp.get(
        '*/api/probe',
        () =>
          new HttpResponse(bytes, {
            headers: { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' },
          }),
      ),
    );

    const result = await http<Blob>('/api/probe');

    expect(result).toBeInstanceOf(Blob);
    expect(result.size).toBe(bytes.length);
  });

  it('parses application/json with a charset', async () => {
    server.use(
      mswHttp.get(
        '*/api/probe',
        () => new HttpResponse('{"value":3}', { headers: { 'Content-Type': 'application/json; charset=utf-8' } }),
      ),
    );

    await expect(http('/api/probe')).resolves.toEqual({ value: 3 });
  });

  it('parses an application/problem+json success body', async () => {
    server.use(
      mswHttp.get(
        '*/api/probe',
        () => new HttpResponse('{"value":4}', { headers: { 'Content-Type': 'application/problem+json' } }),
      ),
    );

    await expect(http('/api/probe')).resolves.toEqual({ value: 4 });
  });

  it('returns a Blob with the lines for a non-empty application/x-ndjson body', async () => {
    const lines = '{"a":1}\n{"a":2}\n';
    server.use(
      mswHttp.get(
        '*/api/probe',
        () => new HttpResponse(lines, { headers: { 'Content-Type': 'application/x-ndjson' } }),
      ),
    );

    const result = await http<Blob>('/api/probe');

    expect(result).toBeInstanceOf(Blob);
    await expect(result.text()).resolves.toBe(lines);
  });

  it('returns an empty Blob for an empty application/x-ndjson body', async () => {
    server.use(
      mswHttp.get('*/api/probe', () => new HttpResponse('', { headers: { 'Content-Type': 'application/x-ndjson' } })),
    );

    const result = await http<Blob>('/api/probe');

    expect(result).toBeInstanceOf(Blob);
    expect(result.size).toBe(0);
  });

  it('rethrows an abort without wrapping it', async () => {
    server.use(mswHttp.get('*/api/probe', () => HttpResponse.json({ value: 1 })));
    const controller = new AbortController();
    controller.abort();

    const error: unknown = await http('/api/probe', { signal: controller.signal }).catch((caught: unknown) => caught);

    expect(error).not.toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ name: 'AbortError' });
  });

  it('sends the access token as a bearer header', async () => {
    setAccessToken('t1');
    server.use(mswHttp.get('*/api/probe', ({ request }) => HttpResponse.json(request.headers.get('authorization'))));

    await expect(http('/api/probe')).resolves.toBe('Bearer t1');
  });

  it('refreshes once and retries a request that returned 401', async () => {
    setAccessToken('expired');
    server.use(
      mswHttp.get('*/api/probe', ({ request }) =>
        request.headers.get('authorization') === 'Bearer fresh'
          ? HttpResponse.json({ value: 2 })
          : new HttpResponse(null, { status: 401 }),
      ),
    );
    registerAuthHandlers({
      refresh: () => {
        setAccessToken('fresh');
        return Promise.resolve(true);
      },
      onExpired: vi.fn(),
    });

    await expect(http('/api/probe')).resolves.toEqual({ value: 2 });
  });

  it('calls onExpired and throws when refresh fails', async () => {
    const onExpired = vi.fn();
    server.use(mswHttp.get('*/api/probe', () => new HttpResponse(null, { status: 401 })));
    registerAuthHandlers({ refresh: () => Promise.resolve(false), onExpired });

    const error: unknown = await http('/api/probe').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 401 });
    expect(onExpired).toHaveBeenCalledOnce();
  });

  it('does not refresh when the refresh endpoint itself returns 401', async () => {
    const refresh = vi.fn(() => Promise.resolve(true));
    server.use(mswHttp.post('*/api/auth/refresh', () => new HttpResponse(null, { status: 401 })));
    registerAuthHandlers({ refresh, onExpired: vi.fn() });

    const error: unknown = await http('/api/auth/refresh', { method: 'POST' }).catch((caught: unknown) => caught);

    expect(error).toMatchObject({ status: 401 });
    expect(refresh).not.toHaveBeenCalled();
  });
});
