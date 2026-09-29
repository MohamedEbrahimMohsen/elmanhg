import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from '@tanstack/react-router';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { ReportClientErrorRequest } from '@/shared/api/generated/model';
import { ApiError } from '@/shared/lib/apiError';
import { clientErrorReporter } from '@/shared/lib/clientErrorReporter';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/renderWithProviders';
import { RouteError } from './RouteError';

let failure: Error | null = null;

function FlakyPage() {
  if (failure) {
    throw failure;
  }
  return <h1>Loaded</h1>;
}

function captureReports(): ReportClientErrorRequest[] {
  const bodies: ReportClientErrorRequest[] = [];
  server.use(
    http.post('*/api/client-errors', async ({ request }) => {
      bodies.push((await request.json()) as ReportClientErrorRequest);
      return new HttpResponse(null, { status: 200 });
    }),
  );
  return bodies;
}

function renderFlakyRouter() {
  const rootRoute = createRootRoute({ component: Outlet });
  const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: FlakyPage });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
    defaultErrorComponent: RouteError,
  });
  return renderWithProviders(<RouterProvider router={router} />);
}

describe('RouteError', () => {
  beforeEach(() => {
    clientErrorReporter.reset();
  });

  afterEach(() => {
    failure = null;
  });

  it('shows the message for the error code and recovers on retry', async () => {
    const user = userEvent.setup();
    failure = new ApiError(0, ['NETWORK_ERROR'], '');
    renderFlakyRouter();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Could not reach the server. Check your connection and try again.',
    );
    failure = null;
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'Loaded' })).toBeInTheDocument();
  });

  it('falls back to the generic message for an unknown code', async () => {
    failure = new ApiError(500, ['NOT_A_KNOWN_CODE'], '');
    renderFlakyRouter();

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
  });

  it('reports an unexpected route error', async () => {
    const bodies = captureReports();
    failure = new Error('boom');
    renderFlakyRouter();

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]).toMatchObject({ message: 'boom', source: 'Route' });
  });

  it('does not report an ApiError', async () => {
    const bodies = captureReports();
    failure = new ApiError(500, ['UNHANDLED_EXCEPTION'], '');
    renderFlakyRouter();

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
    clientErrorReporter.report(new Error('sentinel'), 'Window');
    await waitFor(() => {
      expect(bodies.map((body) => body.message)).toEqual(['sentinel']);
    });
  });
});
