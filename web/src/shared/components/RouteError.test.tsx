import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from '@tanstack/react-router';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import { ApiError } from '@/shared/lib/apiError';
import { renderWithProviders } from '@/test/renderWithProviders';
import { RouteError } from './RouteError';

let failure: ApiError | null = null;

function FlakyPage() {
  if (failure) {
    throw failure;
  }
  return <h1>Loaded</h1>;
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
});
