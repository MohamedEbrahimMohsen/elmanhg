import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Language } from '@/app/i18n';
import { getGetExamAttemptsMockHandler, getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { axe } from '@/test/axe';
import { earlierSittingId, examAttempts, examItem, examSessionId, submittedExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const attemptsTableName = 'Your attempts at this exam, newest first';

async function openResult(lng: Language = 'en') {
  server.use(getGetExamSessionMockHandler(submittedExam([examItem(1)])));
  const rendered = renderApp(`/student/exam-result/${examSessionId}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/exam-result/$sessionId']);
  return rendered;
}

async function attemptsTable() {
  return screen.findByRole('table', { name: attemptsTableName });
}

describe('ExamResultPage attempts', () => {
  it('lists the attempts newest first with the best score', async () => {
    server.use(getGetExamAttemptsMockHandler(examAttempts()));
    await openResult();

    expect(await screen.findByRole('heading', { name: 'Your previous attempts' })).toBeInTheDocument();
    expect(screen.getByText('Best score: 90 / 100')).toBeInTheDocument();
    const rows = within(await attemptsTable())
      .getAllByRole('row')
      .slice(1);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent('80 / 100');
    expect(rows[0]).not.toHaveTextContent('Best');
    expect(rows[1]).toHaveTextContent(/90 \/ 100.*Best/);
  });

  it('marks the attempt being viewed and links the others to their results', async () => {
    server.use(getGetExamAttemptsMockHandler(examAttempts()));
    await openResult();

    const table = await attemptsTable();
    const current = within(table).getByRole('row', { name: /This attempt/ });
    const other = within(table).getByRole('row', { name: /View/ });
    expect(within(current).getByText('This attempt')).toBeInTheDocument();
    expect(within(current).queryByRole('link')).toBeNull();
    expect(within(other).getByRole('link', { name: 'View' })).toHaveAttribute(
      'href',
      `/student/exam-result/${earlierSittingId}`,
    );
  });

  it('shows no attempts card when there are none', async () => {
    await openResult();

    expect(await screen.findByText('80 / 100')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.queryByRole('status', { name: 'Loading your attempts…' })).toBeNull();
    });
    expect(screen.queryByRole('heading', { name: 'Your previous attempts' })).toBeNull();
  });

  it('shows an error with retry when the attempts fail to load', async () => {
    server.use(http.get('*/api/exams/:sessionId/attempts', () => HttpResponse.json({}, { status: 500 })));
    const user = userEvent.setup();
    await openResult();

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load your attempts.')).toBeInTheDocument();
    server.use(getGetExamAttemptsMockHandler(examAttempts()));
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'Your previous attempts' })).toBeInTheDocument();
  });

  it('renders the attempts in Arabic without axe violations', async () => {
    server.use(getGetExamAttemptsMockHandler(examAttempts()));
    const { container } = await openResult('ar');

    expect(await screen.findByRole('heading', { name: 'محاولاتك السابقة' })).toBeInTheDocument();
    expect(screen.getByText('الأفضل')).toBeInTheDocument();
    expect((await axe(container)).violations).toEqual([]);
  });
});
