import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { StudentProfileResult } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import {
  getGetStudentProfileMockHandler,
  getGetStudentProgressMockHandler,
  getGetStudentSessionHistoryMockHandler,
  getGrantComplimentarySubscriptionMockHandler,
} from '@/shared/api/generated/students/students.msw';
import { getSuspendUserMockHandler } from '@/shared/api/generated/users/users.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { planCatalogue } from '@/test/subscriptionFixtures';
import {
  adminSubscription,
  listStudentId,
  studentHistoryItem,
  studentHistoryPage,
  studentProfile,
  studentProgress,
} from '@/test/userFixtures';

const path = `/admin/student/${listStudentId}`;

function serveStudent(profile: () => StudentProfileResult = () => studentProfile()) {
  server.use(
    getGetStudentProfileMockHandler(() => profile()),
    getGetStudentProgressMockHandler(studentProgress()),
    getGetStudentSessionHistoryMockHandler(({ request }) =>
      studentHistoryPage(
        new URL(request.url).searchParams.get('kind') === 'Exam'
          ? [studentHistoryItem({ id: 'exam', kind: 'UnitExam', scopeName: 'Mechanics', isBestScore: true })]
          : [studentHistoryItem()],
      ),
    ),
  );
}

async function openStudent(lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp(path, { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/student/$studentId']);
  return rendered;
}

describe('StudentDetailPage', () => {
  it('shows the profile with the masked contact, plan and complimentary badge', async () => {
    serveStudent(() => studentProfile({ tier: 'Base', subscriptions: [adminSubscription({ isComplimentary: true })] }));
    await openStudent();

    expect(await screen.findByRole('heading', { level: 1, name: 'Mona Ali' })).toBeInTheDocument();
    expect(screen.getByText('010*****678')).toBeInTheDocument();
    const subscriptions = screen.getByRole('table', { name: 'Subscriptions of Mona Ali' });
    expect(within(subscriptions).getByText('Free grant')).toBeInTheDocument();
    expect(within(subscriptions).getByText('Base')).toBeInTheDocument();
  });

  it('shows subject mastery, weak spots and history', async () => {
    serveStudent();
    await openStudent();

    expect(await screen.findByRole('heading', { level: 3, name: 'Physics' })).toBeInTheDocument();
    expect(screen.getByText(/Mastered 8 of 20/)).toBeInTheDocument();
    expect(screen.getByText("Newton's laws", { selector: 'span' })).toBeInTheDocument();
    const history = await screen.findByRole('table', { name: 'Sessions of the student' });
    expect(within(history).getByText('Quiz')).toBeInTheDocument();
  });

  it('filters history to exams', async () => {
    serveStudent();
    const user = userEvent.setup();
    const { router } = await openStudent();

    await screen.findByRole('table', { name: 'Sessions of the student' });
    await user.click(screen.getByRole('button', { name: 'Exams' }));

    const history = await screen.findByRole('table', { name: 'Sessions of the student' });
    expect(await within(history).findByText('Unit exam')).toBeInTheDocument();
    expect(within(history).getByText('Best')).toBeInTheDocument();
    expect(router.state.location.search).toEqual({ kind: 'Exam', page: 1 });
  });

  it('shows the not-found message for STUDENT_NOT_FOUND', async () => {
    serveStudent();
    server.use(
      http.get('*/api/students/:studentId', () =>
        HttpResponse.json({ code: 'STUDENT_NOT_FOUND', message: '' }, { status: 404 }),
      ),
    );
    await openStudent();

    expect(await screen.findByRole('alert')).toHaveTextContent('Student not found.');
  });

  it('shows retry on error', async () => {
    serveStudent();
    server.use(
      http.get('*/api/students/:studentId', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    await openStudent();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the student');
    serveStudent();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Mona Ali' })).toBeInTheDocument();
  });

  it('grants Ask a Teacher to a Base student and shows the toast', async () => {
    const grants: unknown[] = [];
    serveStudent(() => studentProfile({ tier: 'Base' }));
    server.use(
      getGetPlanCatalogueMockHandler(planCatalogue()),
      getGrantComplimentarySubscriptionMockHandler(async ({ request }) => {
        grants.push(await request.json());
        return adminSubscription({ plan: 'AskTeacher', isComplimentary: true });
      }),
    );
    const user = userEvent.setup();
    await openStudent();

    await user.click(await screen.findByRole('button', { name: 'Grant Ask a Teacher' }));
    const dialog = await screen.findByRole('dialog', { name: 'Grant Ask a Teacher to Mona Ali' });
    expect(await within(dialog).findByRole('option', { name: 'Monthly (1 month)' })).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Grant free' }));

    expect(await screen.findByText('Plan granted.')).toBeInTheDocument();
    expect(grants).toEqual([{ plan: 'AskTeacher', period: 'Monthly' }]);
  });

  it('suspends from the profile', async () => {
    let suspended = 0;
    serveStudent();
    server.use(
      getSuspendUserMockHandler(() => {
        suspended += 1;
      }),
    );
    const user = userEvent.setup();
    await openStudent();

    await user.click(await screen.findByRole('button', { name: 'Suspend' }));
    const dialog = await screen.findByRole('dialog', { name: 'Suspend Mona Ali?' });
    await user.click(within(dialog).getByRole('button', { name: 'Suspend' }));

    expect(await screen.findByText('Account suspended.')).toBeInTheDocument();
    expect(suspended).toBe(1);
  });

  it('renders rtl in Arabic', async () => {
    serveStudent();
    await openStudent('ar');

    expect(await screen.findByRole('heading', { level: 2, name: 'الاشتراكات' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
