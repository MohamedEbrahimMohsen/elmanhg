import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { PageDataOfUserSummaryResult } from '@/shared/api/generated/model';
import { getGetUsersMockHandler } from '@/shared/api/generated/users/users.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { userSummary, usersPage } from '@/test/userFixtures';

function respondWith(respond: (request: Request) => PageDataOfUserSummaryResult) {
  server.use(getGetUsersMockHandler(({ request }) => respond(request)));
}

async function openUsers(path = '/admin/users', lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp(path, { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/users']);
  return rendered;
}

describe('UsersPage dates', () => {
  it('shows the joined date in readable Arabic', async () => {
    respondWith(() => usersPage([userSummary()]));
    await openUsers('/admin/users', 'ar');

    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    expect(within(row).getByText('1 سبتمبر 2026')).toBeInTheDocument();
  });
});
