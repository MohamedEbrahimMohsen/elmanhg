import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  getGetUnitExamAttemptsMockHandler,
  getGetUnitExamOverviewMockHandler,
} from '@/shared/api/generated/exams/exams.msw';
import { earlierSittingId, examAttempts, examSessionId, examUnitId, overview } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

async function openStart() {
  server.use(getGetUnitExamOverviewMockHandler(overview()));
  const rendered = renderApp(`/student/exam-start/${examUnitId}`, { session: testSessions.student });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/exam-start/$unitId']);
  return rendered;
}

describe('ExamStartPage attempts', () => {
  it('lists the unit attempts under the start actions', async () => {
    server.use(getGetUnitExamAttemptsMockHandler(examAttempts()));
    await openStart();

    expect(await screen.findByRole('heading', { name: 'Your previous attempts' })).toBeInTheDocument();
    expect(screen.getByText('Best score: 90 / 100')).toBeInTheDocument();
    const table = screen.getByRole('table', { name: 'Your attempts at this exam, newest first' });
    expect(
      within(table)
        .getAllByRole('link', { name: 'View' })
        .map((link) => link.getAttribute('href')),
    ).toEqual([`/student/exam-result/${examSessionId}`, `/student/exam-result/${earlierSittingId}`]);
  });

  it('shows no attempts card before the first sitting', async () => {
    await openStart();

    expect(await screen.findByRole('button', { name: 'Start exam' })).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.queryByRole('status', { name: 'Loading your attempts…' })).toBeNull();
    });
    expect(screen.queryByRole('heading', { name: 'Your previous attempts' })).toBeNull();
  });
});
