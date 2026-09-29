import { waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { RecordFunnelEventRequest } from '@/shared/api/generated/model';
import { server } from '@/test/msw/server';
import { trackFunnelEvent } from './funnelTracker';

const route = '*/api/analytics/funnel-events';

function captureBodies(): RecordFunnelEventRequest[] {
  const bodies: RecordFunnelEventRequest[] = [];
  server.use(
    http.post(route, async ({ request }) => {
      bodies.push((await request.json()) as RecordFunnelEventRequest);
      return new HttpResponse(null, { status: 200 });
    }),
  );
  return bodies;
}

describe('trackFunnelEvent', () => {
  it('posts the event with the same anonymous id every time', async () => {
    const bodies = captureBodies();

    trackFunnelEvent('LandingViewed');
    trackFunnelEvent('SignUpStarted');

    await waitFor(() => {
      expect(bodies.map((body) => body.type).sort()).toEqual(['LandingViewed', 'SignUpStarted']);
    });
    expect(bodies[0]?.anonymousId).toBeTruthy();
    expect(bodies[1]?.anonymousId).toBe(bodies[0]?.anonymousId);
  });

  it('sends a once-only event a single time', async () => {
    const bodies = captureBodies();

    trackFunnelEvent('FirstQuizAnswered', { once: true });
    trackFunnelEvent('FirstQuizAnswered', { once: true });
    trackFunnelEvent('LandingViewed');

    await waitFor(() => {
      expect(bodies.map((body) => body.type).sort()).toEqual(['FirstQuizAnswered', 'LandingViewed']);
    });
  });

  it('swallows a failed request', async () => {
    let calls = 0;
    server.use(
      http.post(route, () => {
        calls += 1;
        return HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 });
      }),
    );

    expect(() => {
      trackFunnelEvent('SignUpCompleted');
    }).not.toThrow();

    await waitFor(() => {
      expect(calls).toBe(1);
    });
  });
});
