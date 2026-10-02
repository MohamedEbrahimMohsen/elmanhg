import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getCreateExamPeriodMockHandler,
  getDeleteExamPeriodMockHandler,
  getGetExamPeriodsMockHandler,
  getGetInfrastructureConfigurationMockHandler,
  getGetRuntimeSettingsMockHandler,
  getUpdateExamPeriodMockHandler,
} from '@/shared/api/generated/configuration/configuration.msw';
import type { ExamPeriodResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import {
  examPeriod,
  infrastructure,
  runtimeSetting,
  settingGroups,
  slaCalendarSetting,
} from '@/test/configurationFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const examPeriodsRoute = '*/api/configuration/exam-periods';

const openPage = (periods: ExamPeriodResult[] = [examPeriod()], lng: 'en' | 'ar' = 'en') => {
  let stored = periods;
  server.use(
    getGetRuntimeSettingsMockHandler(settingGroups(runtimeSetting(), slaCalendarSetting())),
    getGetInfrastructureConfigurationMockHandler(infrastructure()),
    getGetExamPeriodsMockHandler(() => stored),
  );
  const rendered = renderApp('/admin/configuration', { session: testSessions.admin, lng });
  return {
    ...rendered,
    setStored: (next: ExamPeriodResult[]) => {
      stored = next;
    },
  };
};

const dialog = () => screen.findByRole('dialog');

describe('ExamPeriodsSection', () => {
  it('shows the periods under the reply calendar card after loading', async () => {
    openPage();

    const calendar = await screen.findByRole('heading', { level: 2, name: 'Reply calendar' });
    const periods = await screen.findByRole('heading', { level: 2, name: 'Exam periods' });
    expect(calendar.compareDocumentPosition(periods) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(await screen.findByText('Final exams')).toBeInTheDocument();
    expect(screen.getByText('Jun 1, 2026 – Jul 15, 2026')).toBeInTheDocument();
  });

  it('shows the empty state', async () => {
    openPage([]);

    expect(await screen.findByText('No exam periods yet.')).toBeInTheDocument();
  });

  it('shows retry on error and recovers', async () => {
    const user = userEvent.setup();
    openPage();
    server.use(
      http.get(examPeriodsRoute, () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load the exam periods.')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('Final exams')).toBeInTheDocument();
  });

  it('adds a period and shows a toast', async () => {
    const user = userEvent.setup();
    const { setStored } = openPage([]);
    const bodies: unknown[] = [];
    const created = examPeriod({
      id: 'd8d8d8d8-d8d8-4d8d-8d8d-d8d8d8d8d8d8',
      name: 'Second round',
      startDate: '2026-08-01',
      endDate: '2026-08-10',
    });
    server.use(
      getCreateExamPeriodMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        setStored([created]);
        return created;
      }),
    );

    await user.click(await screen.findByRole('button', { name: 'Add exam period' }));
    const form = within(await dialog());
    await user.type(form.getByLabelText('Name'), 'Second round');
    await user.type(form.getByLabelText('First day'), '2026-08-01');
    await user.type(form.getByLabelText('Last day'), '2026-08-10');
    await user.click(form.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Exam period added.')).toBeInTheDocument();
    expect(bodies).toEqual([{ name: 'Second round', startDate: '2026-08-01', endDate: '2026-08-10' }]);
    expect(await screen.findByText('Second round')).toBeInTheDocument();
  });

  it('shows an inline error when the last day is before the first', async () => {
    const user = userEvent.setup();
    openPage([]);
    let posted = false;
    server.use(
      getCreateExamPeriodMockHandler(() => {
        posted = true;
        return examPeriod();
      }),
    );

    await user.click(await screen.findByRole('button', { name: 'Add exam period' }));
    const form = within(await dialog());
    await user.type(form.getByLabelText('Name'), 'Second round');
    await user.type(form.getByLabelText('First day'), '2026-08-10');
    await user.type(form.getByLabelText('Last day'), '2026-08-01');
    await user.click(form.getByRole('button', { name: 'Save' }));

    expect(await form.findByText('The last day must be on or after the first day.')).toBeInTheDocument();
    expect(form.getByLabelText('Last day')).toHaveAttribute('aria-invalid', 'true');
    expect(posted).toBe(false);
  });

  it('shows the server error under the name field', async () => {
    const user = userEvent.setup();
    openPage([]);
    server.use(
      http.post(examPeriodsRoute, () => HttpResponse.json({ code: 'EXAM_PERIOD_NAME_TOO_LONG' }, { status: 422 })),
    );

    await user.click(await screen.findByRole('button', { name: 'Add exam period' }));
    const form = within(await dialog());
    await user.type(form.getByLabelText('Name'), 'Second round');
    await user.type(form.getByLabelText('First day'), '2026-08-01');
    await user.type(form.getByLabelText('Last day'), '2026-08-10');
    await user.click(form.getByRole('button', { name: 'Save' }));

    expect(await form.findByText('The exam period name is too long.')).toBeInTheDocument();
    expect(form.getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true');
  });

  it('edits a period', async () => {
    const user = userEvent.setup();
    openPage();
    const urls: string[] = [];
    server.use(
      getUpdateExamPeriodMockHandler(({ request }) => {
        urls.push(new URL(request.url).pathname);
        return examPeriod({ name: 'Final exams 2026' });
      }),
    );

    await user.click(await screen.findByRole('button', { name: 'Edit Final exams' }));
    const form = within(await dialog());
    expect(form.getByLabelText('First day')).toHaveValue('2026-06-01');
    await user.clear(form.getByLabelText('Name'));
    await user.type(form.getByLabelText('Name'), 'Final exams 2026');
    await user.click(form.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Exam period saved.')).toBeInTheDocument();
    expect(urls).toEqual([`/api/configuration/exam-periods/${examPeriod().id}`]);
  });

  it('deletes a period after confirming', async () => {
    const user = userEvent.setup();
    const { setStored } = openPage();
    const urls: string[] = [];
    server.use(
      getDeleteExamPeriodMockHandler(({ request }) => {
        urls.push(new URL(request.url).pathname);
        setStored([]);
      }),
    );

    await user.click(await screen.findByRole('button', { name: 'Delete Final exams' }));
    const confirm = within(await dialog());
    expect(confirm.getByText(/during Final exams/)).toBeInTheDocument();
    await user.click(confirm.getByRole('button', { name: 'Delete' }));

    expect(await screen.findByText('Exam period deleted.')).toBeInTheDocument();
    expect(urls).toEqual([`/api/configuration/exam-periods/${examPeriod().id}`]);
    expect(await screen.findByText('No exam periods yet.')).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    openPage([examPeriod()], 'ar');

    expect(await screen.findByRole('heading', { level: 2, name: 'فترات الامتحانات' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openPage();

    await screen.findByText('Final exams');
    expect((await axe(container)).violations).toEqual([]);
  });
});
