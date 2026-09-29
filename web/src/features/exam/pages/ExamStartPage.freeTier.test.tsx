import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetUnitExamOverviewMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { examUnitId, overview } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('ExamStartPage free tier', () => {
  it('opens the exam paywall when starting is refused for a free student', async () => {
    server.use(
      getGetUnitExamOverviewMockHandler(overview()),
      http.post('*/api/exams/units/:unitId', () =>
        HttpResponse.json({ code: 'EXAM_REQUIRES_SUBSCRIPTION' }, { status: 403 }),
      ),
    );
    const user = userEvent.setup();
    const rendered = renderApp(`/student/exam-start/${examUnitId}`, { session: testSessions.student });
    await rendered.router.loadRouteChunk(rendered.router.routesById['/student/exam-start/$unitId']);

    await user.click(await screen.findByRole('button', { name: 'Start exam' }));

    expect(await screen.findByText('Exams are included in the Base plan.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Subscribe' })).toHaveAttribute('href', '/student/subscription');
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
