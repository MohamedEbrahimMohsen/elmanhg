import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  getGetAuditLogResourceTypesMockHandler,
  getGetAuditLogsMockHandler,
} from '@/shared/api/generated/audit-logs/audit-logs.msw';
import type { AuditLogResult, PageDataOfAuditLogResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const entry = (overrides: Partial<AuditLogResult> = {}): AuditLogResult => ({
  id: 'e1',
  timestamp: '2026-09-10T08:30:00Z',
  actorUserId: 'a1',
  actorUserName: 'admin@elmanhg.test',
  actorRole: 'Admin',
  action: 'Teacher.AssignSubject',
  resourceType: 'Teacher',
  resourceId: 't1',
  outcome: 'Success',
  errorCode: null,
  traceId: 'trace-1',
  diff: '[{"entityType":"TeacherSubject","change":"Created","properties":{"teacherId":{"before":null,"after":"t1"}}}]',
  ...overrides,
});

const page = (items: AuditLogResult[], pageNumber = 1, totalPages = 1): PageDataOfAuditLogResult => ({
  items,
  pageNumber,
  pageSize: 20,
  totalItems: items.length,
  totalPages,
});

const searchParam = (request: Request, key: string) => new URL(request.url).searchParams.get(key);

const openAuditLog = (path = '/admin/audit', lng: 'en' | 'ar' = 'en') =>
  renderApp(path, { session: testSessions.admin, lng });

describe('AuditLogPage', () => {
  beforeEach(() => {
    server.use(getGetAuditLogResourceTypesMockHandler(['Teacher']));
  });

  it('shows audit entries after loading', async () => {
    server.use(getGetAuditLogsMockHandler(page([entry()])));
    openAuditLog();

    expect(await screen.findByRole('status', { name: 'Loading audit log' })).toBeInTheDocument();
    const row = await screen.findByRole('row', { name: /Teacher\.AssignSubject/ });
    expect(row).toHaveTextContent('admin@elmanhg.test');
  });

  it('shows the empty state when there are no entries', async () => {
    server.use(getGetAuditLogsMockHandler(page([])));
    openAuditLog();

    expect(await screen.findByText('No audit entries yet.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Clear filters' })).toHaveLength(1);
  });

  it('offers clear filters when filters match nothing', async () => {
    server.use(
      getGetAuditLogsMockHandler(({ request }) => page(searchParam(request, 'actor') === null ? [entry()] : [])),
    );
    const user = userEvent.setup();
    const { router } = openAuditLog('/admin/audit?actor=nobody');

    expect(await screen.findByText('No entries match these filters.')).toBeInTheDocument();
    const [, emptyStateClear] = screen.getAllByRole('button', { name: 'Clear filters' });
    if (!emptyStateClear) {
      throw new Error('The empty state does not offer Clear filters.');
    }
    await user.click(emptyStateClear);

    expect(await screen.findByRole('row', { name: /Teacher\.AssignSubject/ })).toBeInTheDocument();
    expect(router.state.location.search).not.toHaveProperty('actor');
  });

  it('shows an error and recovers on retry', async () => {
    server.use(http.get('*/api/audit-logs', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })));
    const user = userEvent.setup();
    openAuditLog();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the audit log');
    server.use(getGetAuditLogsMockHandler(page([entry()])));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('row', { name: /Teacher\.AssignSubject/ })).toBeInTheDocument();
  });

  it('applies filters to the URL and the request', async () => {
    server.use(
      getGetAuditLogsMockHandler(({ request }) =>
        page([
          entry({
            actorUserName: searchParam(request, 'actor') ?? 'unfiltered',
            resourceType: searchParam(request, 'resourceType') ?? 'unfiltered',
          }),
        ]),
      ),
    );
    const user = userEvent.setup();
    const { router } = openAuditLog();

    await screen.findByRole('row', { name: /unfiltered/ });
    await user.type(screen.getByLabelText('Actor'), 'teacher@elmanhg.test');
    await user.selectOptions(screen.getByLabelText('Entity type'), 'Teacher');
    await user.click(screen.getByRole('button', { name: 'Apply filters' }));

    const row = await screen.findByRole('row', { name: /teacher@elmanhg\.test/ });
    expect(row).toHaveTextContent('Teacher');
    expect(row).not.toHaveTextContent('unfiltered');
    expect(router.state.location.search).toEqual({ actor: 'teacher@elmanhg.test', resourceType: 'Teacher', page: 1 });
  });

  it('moves to the next page', async () => {
    server.use(
      getGetAuditLogsMockHandler(({ request }) => {
        const pageNumber = Number(searchParam(request, 'pageNumber') ?? '1');
        return page(
          [entry({ id: `e${String(pageNumber)}`, action: `Page${String(pageNumber)}.Entry` })],
          pageNumber,
          2,
        );
      }),
    );
    const user = userEvent.setup();
    openAuditLog();

    expect(await screen.findByText('Page 1 of 2')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByRole('row', { name: /Page2\.Entry/ })).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled();
  });

  it('shows an inline error when the end date is before the start date', async () => {
    server.use(getGetAuditLogsMockHandler(page([entry()])));
    const user = userEvent.setup();
    const { router } = openAuditLog();

    await screen.findByRole('row', { name: /Teacher\.AssignSubject/ });
    await user.type(screen.getByLabelText('From'), '2026-09-10');
    await user.type(screen.getByLabelText('To'), '2026-09-01');
    await user.click(screen.getByRole('button', { name: 'Apply filters' }));

    expect(await screen.findByText('End date must be on or after the start date.')).toBeInTheDocument();
    expect(router.state.location.search).toEqual({});
  });

  it('expands the changes of an entry', async () => {
    server.use(getGetAuditLogsMockHandler(page([entry()])));
    const user = userEvent.setup();
    openAuditLog();

    await user.click(await screen.findByText('Show changes'));

    expect(screen.getByText(/"teacherId"/)).toBeVisible();
  });

  it('renders right-to-left in Arabic', async () => {
    server.use(getGetAuditLogsMockHandler(page([entry()])));
    openAuditLog('/admin/audit', 'ar');

    expect(await screen.findByRole('heading', { name: 'سجل التدقيق' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const failure = entry({
      id: 'e2',
      actorUserName: null,
      actorRole: null,
      outcome: 'Failure',
      errorCode: 'SUBJECT_NOT_FOUND',
      diff: null,
    });
    server.use(getGetAuditLogsMockHandler(page([entry(), failure])));
    const { container } = openAuditLog();

    await screen.findByRole('table');

    expect((await axe(container)).violations).toEqual([]);
  });
});
