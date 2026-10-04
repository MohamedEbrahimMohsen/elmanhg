import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { PageDataOfUserSummaryResult } from '@/shared/api/generated/model';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { getGetUsersMockHandler } from '@/shared/api/generated/users/users.msw';
import { axe } from '@/test/axe';
import { mintButtons } from '@/test/mintButtons';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { listTeacherId, userSummary, usersPage } from '@/test/userFixtures';

const searchParam = (request: Request, key: string) => new URL(request.url).searchParams.get(key);

function respondWith(respond: (request: Request) => PageDataOfUserSummaryResult) {
  server.use(getGetUsersMockHandler(({ request }) => respond(request)));
}

async function openUsers(path = '/admin/users', lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp(path, { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/users']);
  return rendered;
}

describe('UsersPage', () => {
  it('shows students after loading', async () => {
    respondWith(() => usersPage([userSummary()]));
    await openUsers();

    expect(await screen.findByRole('status', { name: 'Loading users…' })).toBeInTheDocument();
    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    expect(within(row).getByText('010*****678')).toBeInTheDocument();
    expect(within(row).getByText('Free')).toBeInTheDocument();
  });

  it('shows the empty state when there are no users', async () => {
    respondWith(() => usersPage([]));
    await openUsers();

    expect(await screen.findByText('No users yet.')).toBeInTheDocument();
  });

  it('shows no-results with Clear filters that clears the URL', async () => {
    respondWith((request) => usersPage(searchParam(request, 'search') === null ? [userSummary()] : []));
    const user = userEvent.setup();
    const { router } = await openUsers('/admin/users?q=Nobody');

    expect(await screen.findByText('No users match the search.')).toBeInTheDocument();
    expect(mintButtons()).toHaveLength(0);
    const clear = screen.getAllByRole('button', { name: 'Clear filters' }).at(-1);
    if (!clear) {
      throw new Error('The empty state does not offer Clear filters.');
    }
    await user.click(clear);

    expect(await screen.findByRole('row', { name: /Mona Ali/ })).toBeInTheDocument();
    expect(router.state.location.search).not.toHaveProperty('q');
  });

  it('keeps Invite as the only mint button on a filtered empty Teachers tab', async () => {
    respondWith(() => usersPage([]));
    await openUsers('/admin/users?tab=teachers&q=Nobody');

    expect(await screen.findByText('No users match the search.')).toBeInTheDocument();
    expect(mintButtons().map((button) => button.textContent)).toEqual(['Invite teacher']);
  });

  it('shows retry on error and recovers', async () => {
    server.use(http.get('*/api/users', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })));
    const user = userEvent.setup();
    await openUsers();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load users');
    respondWith(() => usersPage([userSummary()]));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('row', { name: /Mona Ali/ })).toBeInTheDocument();
  });

  it('switching to Teachers requests role=Teacher and shows subject checkboxes', async () => {
    const roles: (string | null)[] = [];
    respondWith((request) => {
      roles.push(searchParam(request, 'role'));
      return searchParam(request, 'role') === 'Teacher'
        ? usersPage([
            userSummary({
              id: listTeacherId,
              displayName: 'Omar Teacher',
              role: 'Teacher',
              tier: null,
              subjectIds: ['s-physics'],
            }),
          ])
        : usersPage([userSummary()]);
    });
    server.use(getGetSubjectsMockHandler([{ id: 's-physics', name: 'Physics', order: 1, unitCount: 1 }]));
    const user = userEvent.setup();
    await openUsers();

    await screen.findByRole('row', { name: /Mona Ali/ });
    await user.click(screen.getByRole('tab', { name: 'Teachers' }));

    const row = await screen.findByRole('row', { name: /Omar Teacher/ });
    expect(await within(row).findByRole('checkbox', { name: 'Physics' })).toBeChecked();
    expect(roles).toContain('Teacher');
  });

  it('applying a search puts q in the request', async () => {
    respondWith((request) =>
      usersPage([userSummary({ displayName: `Search ${searchParam(request, 'search') ?? 'none'}` })]),
    );
    const user = userEvent.setup();
    const { router } = await openUsers();

    await screen.findByRole('row', { name: /Search none/ });
    await user.type(screen.getByLabelText('Search'), '01012345678');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(await screen.findByRole('row', { name: /Search 01012345678/ })).toBeInTheDocument();
    expect(router.state.location.search).toEqual({ page: 1, q: '01012345678' });
  });

  it('renders rtl in Arabic', async () => {
    respondWith(() => usersPage([userSummary()]));
    await openUsers('/admin/users', 'ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'المستخدمون' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    respondWith(() =>
      usersPage([userSummary(), userSummary({ id: 'second', displayName: 'Suspended One', status: 'Suspended' })]),
    );
    const { container } = await openUsers();

    await screen.findByRole('table');

    expect((await axe(container)).violations).toEqual([]);
  });
});
