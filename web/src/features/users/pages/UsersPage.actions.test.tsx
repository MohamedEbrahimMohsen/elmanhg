import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { UserSummaryResult } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGrantComplimentarySubscriptionMockHandler } from '@/shared/api/generated/students/students.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import {
  getAssignTeacherSubjectMockHandler,
  getUnassignTeacherSubjectMockHandler,
} from '@/shared/api/generated/teachers/teachers.msw';
import {
  getGetUsersMockHandler,
  getInviteUserMockHandler,
  getReactivateUserMockHandler,
  getSuspendUserMockHandler,
} from '@/shared/api/generated/users/users.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { planCatalogue } from '@/test/subscriptionFixtures';
import {
  adminSubscription,
  listAdminId,
  listStudentId,
  listTeacherId,
  userSummary,
  usersPage,
} from '@/test/userFixtures';

const physics = { id: 'f7f7f7f7-f7f7-4f7f-8f7f-f7f7f7f7f7f7', name: 'Physics', order: 1, unitCount: 1 };

function serveUsers(current: () => UserSummaryResult[]) {
  server.use(getGetUsersMockHandler(() => usersPage(current())));
}

async function openUsers(path = '/admin/users') {
  const rendered = renderApp(path, { session: testSessions.admin, lng: 'en' });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/users']);
  return rendered;
}

const admin = (overrides: Partial<UserSummaryResult>) =>
  userSummary({
    id: listAdminId,
    displayName: 'Other Admin',
    role: 'Admin',
    tier: null,
    maskedPhone: null,
    maskedEmail: 'o***@example.test',
    ...overrides,
  });

const teacher = (subjectIds: string[]) =>
  userSummary({ id: listTeacherId, displayName: 'Omar Teacher', role: 'Teacher', tier: null, subjectIds });

describe('UsersPage actions', () => {
  it('suspends a student after confirming and shows the toast', async () => {
    let student = userSummary();
    const suspended: string[] = [];
    serveUsers(() => [student]);
    server.use(
      getSuspendUserMockHandler(({ params }) => {
        suspended.push(String(params.userId));
        student = userSummary({ status: 'Suspended', canSuspend: false });
      }),
    );
    const user = userEvent.setup();
    await openUsers();

    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    await user.click(within(row).getByRole('button', { name: 'Suspend' }));
    const dialog = await screen.findByRole('dialog', { name: 'Suspend Mona Ali?' });
    await user.click(within(dialog).getByRole('button', { name: 'Suspend' }));

    expect(await screen.findByText('Account suspended.')).toBeInTheDocument();
    expect(suspended).toEqual([listStudentId]);
    expect(
      await within(await screen.findByRole('row', { name: /Mona Ali/ })).findByText('Suspended'),
    ).toBeInTheDocument();
  });

  it('activates a suspended user', async () => {
    let reactivated = 0;
    serveUsers(() => [userSummary({ status: 'Suspended', canSuspend: false })]);
    server.use(
      getReactivateUserMockHandler(() => {
        reactivated += 1;
      }),
    );
    const user = userEvent.setup();
    await openUsers();

    await user.click(await screen.findByRole('button', { name: 'Activate' }));
    const dialog = await screen.findByRole('dialog', { name: 'Activate Mona Ali?' });
    await user.click(within(dialog).getByRole('button', { name: 'Activate' }));

    expect(await screen.findByText('Account activated.')).toBeInTheDocument();
    expect(reactivated).toBe(1);
  });

  it('disables deactivate for the last active admin with the hint', async () => {
    serveUsers(() => [admin({ canSuspend: false })]);
    await openUsers('/admin/users?tab=admins');

    const row = await screen.findByRole('row', { name: /Other Admin/ });
    expect(within(row).getByRole('button', { name: 'Deactivate' })).toBeDisabled();
    expect(within(row).getByText('Last active admin')).toBeInTheDocument();
  });

  it("disables deactivate on the current admin's own row", async () => {
    serveUsers(() => [admin({ id: testSessions.admin.userId, displayName: 'Me Admin', canSuspend: false })]);
    await openUsers('/admin/users?tab=admins');

    const row = await screen.findByRole('row', { name: /Me Admin/ });
    expect(within(row).getByRole('button', { name: 'Deactivate' })).toBeDisabled();
    expect(within(row).getByText('This is your account')).toBeInTheDocument();
  });

  it('assigning a subject posts to the assign endpoint', async () => {
    const assigned: string[] = [];
    serveUsers(() => [teacher([])]);
    server.use(
      getGetSubjectsMockHandler([physics]),
      getAssignTeacherSubjectMockHandler(({ params }) => {
        assigned.push(`${String(params.teacherId)}/${String(params.subjectId)}`);
        return { teacherId: listTeacherId, subjectId: physics.id, assignedAt: '2026-09-30T10:00:00Z' };
      }),
    );
    const user = userEvent.setup();
    await openUsers('/admin/users?tab=teachers');

    await user.click(await screen.findByRole('checkbox', { name: 'Physics' }));

    expect(await screen.findByText('Subjects updated.')).toBeInTheDocument();
    expect(assigned).toEqual([`${listTeacherId}/${physics.id}`]);
  });

  it('unchecking a subject calls unassign', async () => {
    const unassigned: string[] = [];
    serveUsers(() => [teacher([physics.id])]);
    server.use(
      getGetSubjectsMockHandler([physics]),
      getUnassignTeacherSubjectMockHandler(({ params }) => {
        unassigned.push(String(params.subjectId));
      }),
    );
    const user = userEvent.setup();
    await openUsers('/admin/users?tab=teachers');

    await user.click(await screen.findByRole('checkbox', { name: 'Physics' }));

    await waitFor(() => {
      expect(unassigned).toEqual([physics.id]);
    });
  });

  it('the invite teacher dialog shows the invitation link after success', async () => {
    const bodies: unknown[] = [];
    serveUsers(() => []);
    server.use(
      getInviteUserMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        return { userId: listTeacherId, emailSent: false };
      }),
    );
    const user = userEvent.setup();
    await openUsers('/admin/users?tab=teachers');

    await user.click(await screen.findByRole('button', { name: 'Invite teacher' }));
    const dialog = await screen.findByRole('dialog', { name: 'Invite teacher' });
    await user.type(within(dialog).getByLabelText('Name'), 'Omar');
    await user.type(within(dialog).getByLabelText('Email'), 'omar@example.test');
    await user.click(within(dialog).getByRole('button', { name: 'Create invitation' }));

    const done = await screen.findByRole('dialog', { name: 'Invitation created' });
    expect(within(done).getByLabelText('Invitation link')).toHaveValue(
      new URL('/accept-invite', document.baseURI).href,
    );
    expect(
      within(done).getByText('The invitation email was not sent, so share the link yourself.'),
    ).toBeInTheDocument();
    expect(bodies).toEqual([{ role: 'Teacher', displayName: 'Omar', email: 'omar@example.test' }]);
  });

  it('the invite shows EMAIL_ALREADY_REGISTERED under the email field', async () => {
    serveUsers(() => []);
    server.use(
      http.post('*/api/users/invitations', () =>
        HttpResponse.json({ code: 'EMAIL_ALREADY_REGISTERED', message: '' }, { status: 409 }),
      ),
    );
    const user = userEvent.setup();
    await openUsers('/admin/users?tab=admins');

    await user.click(await screen.findByRole('button', { name: 'Invite admin' }));
    const dialog = await screen.findByRole('dialog', { name: 'Invite admin' });
    await user.type(within(dialog).getByLabelText('Name'), 'Sara');
    await user.type(within(dialog).getByLabelText('Email'), 'sara@example.test');
    await user.click(within(dialog).getByRole('button', { name: 'Create invitation' }));

    expect(await within(dialog).findByText('An account already uses this email.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('a LAST_ACTIVE_ADMIN server error shows an error toast', async () => {
    serveUsers(() => [admin({ canSuspend: true })]);
    server.use(
      http.post('*/api/users/:userId/suspend', () =>
        HttpResponse.json({ code: 'LAST_ACTIVE_ADMIN', message: '' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    await openUsers('/admin/users?tab=admins');

    await user.click(await screen.findByRole('button', { name: 'Deactivate' }));
    const dialog = await screen.findByRole('dialog', { name: 'Deactivate Other Admin?' });
    await user.click(within(dialog).getByRole('button', { name: 'Deactivate' }));

    expect(await screen.findByText('At least one active admin must remain.')).toBeInTheDocument();
  });

  it("grants Base from a Free student's row", async () => {
    const grants: unknown[] = [];
    serveUsers(() => [userSummary()]);
    server.use(
      getGetPlanCatalogueMockHandler(planCatalogue()),
      getGrantComplimentarySubscriptionMockHandler(async ({ request }) => {
        grants.push(await request.json());
        return adminSubscription({ isComplimentary: true, period: 'Termly' });
      }),
    );
    const user = userEvent.setup();
    await openUsers();

    await user.click(await screen.findByRole('button', { name: 'Grant Base' }));
    const dialog = await screen.findByRole('dialog', { name: 'Grant Base to Mona Ali' });
    await user.selectOptions(await within(dialog).findByLabelText('Period'), 'Termly');
    await user.click(within(dialog).getByRole('button', { name: 'Grant free' }));

    expect(await screen.findByText('Plan granted.')).toBeInTheDocument();
    expect(grants).toEqual([{ plan: 'Base', period: 'Termly' }]);
  });
});
