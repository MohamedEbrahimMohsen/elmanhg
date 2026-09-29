import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getGetMultiUnitExamOverviewMockHandler,
  getPreviewMultiUnitExamMockHandler,
} from '@/shared/api/generated/exams/exams.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { examSubjectId, multiOverview, multiPreview } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('MultiExamBuilderPage free tier', () => {
  it('opens the exam paywall when the multi-unit start is refused', async () => {
    server.use(
      getGetSubjectsMockHandler([{ id: examSubjectId, name: 'Physics', order: 1, unitCount: 2 }]),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      getPreviewMultiUnitExamMockHandler(multiPreview()),
      http.post('*/api/exams/subjects/:subjectId/multi-unit', () =>
        HttpResponse.json({ code: 'EXAM_REQUIRES_SUBSCRIPTION' }, { status: 403 }),
      ),
    );
    const user = userEvent.setup();
    const rendered = renderApp('/student/multi-exam', { session: testSessions.student });
    await rendered.router.loadRouteChunk(rendered.router.routesById['/student/multi-exam']);
    await user.click(await screen.findByRole('checkbox', { name: 'Mechanics' }));
    await user.click(screen.getByRole('checkbox', { name: 'Waves' }));

    await user.click(await screen.findByRole('button', { name: 'Start exam' }));

    expect(await screen.findByText('Exams are included in the Base plan.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Subscribe' })).toHaveAttribute('href', '/student/subscription');
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
