import { waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { ReportClientErrorRequest } from '@/shared/api/generated/model';
import { ApiError } from '@/shared/lib/apiError';
import { server } from '@/test/msw/server';
import { clientErrorLimits, createClientErrorReporter, maxReportsPerPage } from './clientErrorReporter';

const route = '*/api/client-errors';

function captureBodies(): ReportClientErrorRequest[] {
  const bodies: ReportClientErrorRequest[] = [];
  server.use(
    http.post(route, async ({ request }) => {
      bodies.push((await request.json()) as ReportClientErrorRequest);
      return new HttpResponse(null, { status: 200 });
    }),
  );
  return bodies;
}

async function expectMessages(bodies: ReportClientErrorRequest[], messages: string[]) {
  await waitFor(() => {
    expect(bodies.map((body) => body.message).sort()).toEqual([...messages].sort());
  });
}

describe('clientErrorReporter', () => {
  const startPath = window.location.pathname;

  beforeEach(() => {
    window.history.replaceState(null, '', '/student/quiz?token=secret#answer');
  });

  afterEach(() => {
    window.history.replaceState(null, '', startPath);
  });

  it('posts message, name, stack, source and path when an error is reported', async () => {
    const bodies = captureBodies();
    const error = new TypeError('x is undefined');

    createClientErrorReporter().report(error, 'Window');

    await expectMessages(bodies, ['x is undefined']);
    expect(bodies[0]).toEqual({
      message: 'x is undefined',
      errorName: 'TypeError',
      stack: error.stack,
      source: 'Window',
      path: '/student/quiz',
    });
  });

  it('truncates message and stack to the server limits', async () => {
    const bodies = captureBodies();
    const error = new Error('m'.repeat(clientErrorLimits.message + 50));
    error.stack = 's'.repeat(clientErrorLimits.stack + 50);

    createClientErrorReporter().report(error, 'Window');

    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.message).toHaveLength(clientErrorLimits.message);
    expect(bodies[0]?.stack).toHaveLength(clientErrorLimits.stack);
  });

  it('does not report an ApiError or an AbortError', async () => {
    const bodies = captureBodies();
    const reporter = createClientErrorReporter();

    reporter.report(new ApiError(500, ['UNHANDLED_EXCEPTION'], 'server'), 'Window');
    reporter.report(new DOMException('aborted', 'AbortError'), 'UnhandledRejection');
    reporter.report(new Error('sentinel'), 'Window');

    await expectMessages(bodies, ['sentinel']);
  });

  it('sends a repeated error only once', async () => {
    const bodies = captureBodies();
    const reporter = createClientErrorReporter();

    reporter.report(new Error('same'), 'Window');
    reporter.report(new Error('same'), 'Window');
    reporter.report(new Error('sentinel'), 'Window');

    await expectMessages(bodies, ['same', 'sentinel']);
  });

  it('stops after the per-page limit', async () => {
    const bodies = captureBodies();
    const reporter = createClientErrorReporter();

    for (let index = 0; index < maxReportsPerPage + 2; index++) {
      reporter.report(new Error(`error ${String(index)}`), 'Window');
    }

    await waitFor(() => {
      expect(bodies).toHaveLength(maxReportsPerPage);
    });
    expect(bodies.map((body) => body.message)).not.toContain(`error ${String(maxReportsPerPage)}`);
  });

  it('reports window errors and unhandled rejections once installed', async () => {
    const bodies = captureBodies();
    const remove = createClientErrorReporter().install(window);

    window.dispatchEvent(new ErrorEvent('error', { error: new Error('window failure'), message: 'window failure' }));
    const rejection = new Event('unhandledrejection');
    Object.defineProperty(rejection, 'reason', { value: new Error('rejected promise') });
    window.dispatchEvent(rejection);
    remove();

    await expectMessages(bodies, ['rejected promise', 'window failure']);
    expect(bodies.find((body) => body.message === 'window failure')?.source).toBe('Window');
    expect(bodies.find((body) => body.message === 'rejected promise')?.source).toBe('UnhandledRejection');
  });

  it('stops listening after cleanup', async () => {
    const bodies = captureBodies();
    const reporter = createClientErrorReporter();
    const remove = reporter.install(window);

    remove();
    // plain events: after cleanup no listener handles them, and vitest rethrows an unhandled ErrorEvent.error
    window.dispatchEvent(new Event('error'));
    window.dispatchEvent(new Event('unhandledrejection'));
    reporter.report(new Error('sentinel'), 'Window');

    await expectMessages(bodies, ['sentinel']);
  });

  it('ignores a failed report', async () => {
    let calls = 0;
    server.use(
      http.post(route, () => {
        calls += 1;
        return HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 });
      }),
    );

    expect(() => {
      createClientErrorReporter().report(new Error('boom'), 'Window');
    }).not.toThrow();

    await waitFor(() => {
      expect(calls).toBe(1);
    });
  });
});
