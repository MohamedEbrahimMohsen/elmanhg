import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { ValidationQueueItemResult } from '@/shared/api/generated/model';
import {
  getGetValidationQueueFiltersMockHandler,
  getGetValidationQueueMockHandler,
  getStartReviewSessionMockHandler,
} from '@/shared/api/generated/validation-queue/validation-queue.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const subjectId = '22222222-2222-4222-8222-222222222222';
const unitId = '33333333-3333-4333-8333-333333333333';
const lessonId = '44444444-4444-4444-8444-444444444444';

const item: ValidationQueueItemResult = {
  id: '55555555-5555-4555-8555-555555555555',
  subjectId,
  unitId,
  unitName: 'Mechanics',
  lessonId,
  lessonName: "Newton's laws",
  type: 'Mcq',
  stem: '<p>What is the unit of force?</p>',
  difficulty: 'Medium',
  version: 1,
  submittedAt: '2026-09-20T10:00:00Z',
  openedInSession: true,
};

async function region(name: string) {
  return within(await screen.findByRole('region', { name }));
}

describe('ValidationQueuePage stats card', () => {
  beforeEach(() => {
    server.use(
      getStartReviewSessionMockHandler({
        reviewSessionId: '11111111-1111-4111-8111-111111111111',
        expiresAt: '2026-09-28T20:00:00Z',
      }),
      getGetValidationQueueFiltersMockHandler({
        subjects: [{ id: subjectId, name: 'Physics' }],
        units: [{ id: unitId, subjectId, name: 'Mechanics' }],
        lessons: [{ id: lessonId, unitId, name: "Newton's laws" }],
      }),
      getGetValidationQueueMockHandler({ items: [item], pageNumber: 1, pageSize: 20, totalItems: 1, totalPages: 1 }),
    );
  });

  it('shows my stats on the teacher home', async () => {
    renderApp('/teacher', { session: testSessions.teacher });

    expect(await screen.findByRole('heading', { level: 1, name: 'Review queue' })).toBeInTheDocument();
    const card = await region('My reviews and replies');
    expect(await card.findByText('Approved: 12')).toBeInTheDocument();
    expect(card.getByText('Reply SLA compliance: 92.5%')).toBeInTheDocument();
  });

  it('keeps the queue usable when my stats fail', async () => {
    server.use(
      http.get('*/api/dashboard/my-stats', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    renderApp('/teacher', { session: testSessions.teacher });

    const card = await region('My reviews and replies');
    expect(await card.findByRole('alert')).toHaveTextContent('Could not load My reviews and replies');
    expect(await screen.findByRole('list', { name: 'Questions waiting for review' })).toBeInTheDocument();
  });
});
