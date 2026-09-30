import { screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import type { ValidationQuestionDetailResult } from '@/shared/api/generated/model';
import {
  getGetValidationQuestionMockHandler,
  getGetValidationQueueFiltersMockHandler,
  getGetValidationQueueMockHandler,
  getRecordQuestionOpeningMockHandler,
  getStartReviewSessionMockHandler,
} from '@/shared/api/generated/validation-queue/validation-queue.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const questionId = '22222222-2222-4222-8222-222222222222';

const key = 'question-diagrams/55555555-5555-4555-8555-555555555555/0123456789abcdef0123456789abcdef.png';

const dragDrop: ValidationQuestionDetailResult = {
  id: questionId,
  subjectId: '33333333-3333-4333-8333-333333333333',
  subjectName: 'Biology',
  unitId: '44444444-4444-4444-8444-444444444444',
  unitName: 'Cells',
  lessonId: '55555555-5555-4555-8555-555555555555',
  lessonName: 'Plant cells',
  lessonState: 'Draft',
  type: 'DragDrop',
  stem: '<p>Label the plant cell.</p>',
  body: {
    image: { key, url: `/api/media/${key}`, width: 800, height: 600, alt: 'Plant cell' },
    zones: [
      { id: 'z1', x: 10, y: 10, width: 20, height: 15, capacity: 2 },
      { id: 'z2', x: 50, y: 40, width: 30, height: 20.5, capacity: 2 },
    ],
    items: [
      { id: 'i1', text: 'Nucleus' },
      { id: 'i2', text: 'Vacuole' },
      { id: 'i3', text: 'Wall' },
      { id: 'i4', text: 'Membrane' },
      { id: 'i5', text: 'Engine' },
    ],
  },
  gradingSpec: {
    zones: [
      { zoneId: 'z1', itemIds: ['i1', 'i2'], ordered: false },
      { zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true },
    ],
  },
  explanation: '<p>Parts of a cell.</p>',
  difficulty: 'Medium',
  objectiveId: null,
  objectiveText: null,
  tags: [],
  maxScore: 4,
  version: 1,
  validationStatus: 'Pending',
  rejectionReason: null,
  submittedAt: '2026-09-22T10:00:00Z',
  retiredAt: null,
  revisions: [{ version: 1, editedAt: '2026-09-22T10:00:00Z' }],
  decisions: [],
};

async function openDragDrop(lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp(`/teacher/q/${questionId}`, { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/q/$questionId']);
  return rendered;
}

describe('ValidationQuestionPage drag and drop', () => {
  beforeEach(() => {
    server.use(
      getStartReviewSessionMockHandler({
        reviewSessionId: '11111111-1111-4111-8111-111111111111',
        expiresAt: '2026-09-28T20:00:00Z',
      }),
      getGetValidationQuestionMockHandler(dragDrop),
      getRecordQuestionOpeningMockHandler(),
      getGetValidationQueueFiltersMockHandler({ subjects: [], units: [], lessons: [] }),
      getGetValidationQueueMockHandler({ items: [], pageNumber: 1, pageSize: 20, totalItems: 0, totalPages: 0 }),
    );
  });

  it('shows the diagram with its correct placements', async () => {
    await openDragDrop();

    const key = within(await screen.findByRole('region', { name: 'Correct placements' }));
    expect(key.getByText('Zone 1: Nucleus, Vacuole')).toBeInTheDocument();
    expect(key.getByText('Zone 2, in order: Membrane → Wall')).toBeInTheDocument();
    expect(key.getByText('Distractors (stay unplaced): Engine')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Plant cell' })).toBeInTheDocument();
  });

  it('renders the review right-to-left in Arabic', async () => {
    await openDragDrop('ar');

    expect(await screen.findByRole('region', { name: 'الأماكن الصحيحة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = await openDragDrop();

    await screen.findByRole('region', { name: 'Correct placements' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
