import { http as mswHttp, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { server } from '@/test/msw/server';
import { ApiError } from './apiError';
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

  it('rethrows an abort without wrapping it', async () => {
    server.use(mswHttp.get('*/api/probe', () => HttpResponse.json({ value: 1 })));
    const controller = new AbortController();
    controller.abort();

    const error: unknown = await http('/api/probe', { signal: controller.signal }).catch((caught: unknown) => caught);

    expect(error).not.toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ name: 'AbortError' });
  });
});
