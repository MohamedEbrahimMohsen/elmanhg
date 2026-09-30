import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getGetLessonMockHandler, getUploadDiagramImageMockHandler } from '@/shared/api/generated/lessons/lessons.msw';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import {
  getCreateQuestionMockHandler,
  getGradeQuestionDraftMockHandler,
} from '@/shared/api/generated/questions/questions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lessonId = '11111111-1111-4111-8111-111111111111';
const questionId = '22222222-2222-4222-8222-222222222222';
const DIAGRAM_KEY = 'question-diagrams/11111111-1111-4111-8111-111111111111/0123456789abcdef0123456789abcdef.png';
const DIAGRAM_URL = `/api/media/${DIAGRAM_KEY}`;

const lesson: LessonDetailResult = {
  id: lessonId,
  unitId: '33333333-3333-4333-8333-333333333333',
  name: 'Plant cells',
  order: 1,
  state: 'Draft',
  explanation: '',
  summary: '',
  videoUrl: null,
  objectives: [],
};

async function openDragDropEditor(user: UserEvent) {
  const rendered = renderApp(`/admin/question/new/${lessonId}`, { session: testSessions.admin });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/question/new/$lessonId']);
  await user.selectOptions(await screen.findByLabelText('Type'), 'Drag and drop (v2)');
  return rendered;
}

async function uploadDiagram(user: UserEvent) {
  await user.upload(
    screen.getByLabelText('Diagram image (PNG, JPG or WEBP)'),
    new File(['png'], 'cell.png', { type: 'image/png' }),
  );
}

async function replace(user: UserEvent, label: string, text: string) {
  const input = screen.getByLabelText(label);
  await user.clear(input);
  await user.type(input, text);
}

async function drawZone(user: UserEvent) {
  const canvas = await screen.findByRole('img', { name: 'Diagram canvas' });
  await user.pointer([
    { keys: '[MouseLeft>]', target: canvas, coords: { clientX: 40, clientY: 30 } },
    { target: canvas, coords: { clientX: 200, clientY: 150 } },
    { keys: '[/MouseLeft]', target: canvas, coords: { clientX: 200, clientY: 150 } },
  ]);
}

async function fillBasics(user: UserEvent) {
  await user.click(screen.getByRole('textbox', { name: 'Question text' }));
  await user.type(screen.getByRole('textbox', { name: 'Question text' }), 'Label the plant cell.');
  await uploadDiagram(user);
  await screen.findByText('Current image: 800 × 600 px');
  await replace(user, 'Image description (for screen readers)', 'Plant cell');
}

describe('NewQuestionPage drag and drop', () => {
  let created: unknown[];

  beforeEach(() => {
    created = [];
    vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ width: 800, height: 600, close: vi.fn() }));
    vi.spyOn(SVGElement.prototype, 'getBoundingClientRect').mockReturnValue({
      x: 0,
      y: 0,
      left: 0,
      top: 0,
      right: 400,
      bottom: 300,
      width: 400,
      height: 300,
      toJSON: () => ({}),
    });
    server.use(
      getGetLessonMockHandler(lesson),
      getUploadDiagramImageMockHandler({ key: DIAGRAM_KEY, url: DIAGRAM_URL }),
      getCreateQuestionMockHandler(async ({ request }) => {
        created.push(await request.json());
        return { id: questionId };
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('creates a question from an uploaded diagram, a drawn zone and placed items', async () => {
    const user = userEvent.setup();
    await openDragDropEditor(user);

    await fillBasics(user);
    await drawZone(user);
    expect(screen.getByLabelText('Zone 1: from left (%)')).toHaveValue('10');
    expect(screen.getByLabelText('Zone 1: from top (%)')).toHaveValue('10');
    expect(screen.getByLabelText('Zone 1: width (%)')).toHaveValue('40');
    expect(screen.getByLabelText('Zone 1: height (%)')).toHaveValue('40');
    await user.type(screen.getByLabelText('Item 1'), 'Nucleus');
    await user.selectOptions(screen.getByLabelText('Correct zone for item 1'), 'Zone 1');
    await user.click(screen.getByRole('button', { name: 'Add item' }));
    await user.type(screen.getByLabelText('Item 2'), 'Engine');
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    await waitFor(() => {
      expect(created).toHaveLength(1);
    });
    expect(created[0]).toEqual(
      expect.objectContaining({
        type: 'DragDrop',
        body: {
          image: { key: DIAGRAM_KEY, width: 800, height: 600, alt: 'Plant cell' },
          zones: [{ id: 'z1', x: 10, y: 10, width: 40, height: 40, capacity: 1 }],
          items: [
            { id: 'i1', text: 'Nucleus' },
            { id: 'i2', text: 'Engine' },
          ],
        },
        gradingSpec: { zones: [{ zoneId: 'z1', itemIds: ['i1'], ordered: false }] },
      }),
    );
  });

  it('adds a zone from the keyboard and edits its position', async () => {
    const user = userEvent.setup();
    await openDragDropEditor(user);
    await fillBasics(user);

    await user.click(screen.getByRole('button', { name: 'Add zone' }));
    expect(screen.getByLabelText('Zone 1: from left (%)')).toHaveValue('40');
    expect(screen.getByLabelText('Zone 1: from top (%)')).toHaveValue('40');
    expect(screen.getByLabelText('Zone 1: width (%)')).toHaveValue('20');
    expect(screen.getByLabelText('Zone 1: height (%)')).toHaveValue('20');
    await replace(user, 'Zone 1: from left (%)', '5');
    await user.type(screen.getByLabelText('Item 1'), 'Nucleus');
    await user.selectOptions(screen.getByLabelText('Correct zone for item 1'), 'Zone 1');
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    await waitFor(() => {
      expect(created).toHaveLength(1);
    });
    expect(created[0]).toMatchObject({
      body: { zones: [{ id: 'z1', x: 5, y: 40, width: 20, height: 20, capacity: 1 }] },
    });
  });

  it('keeps the authored order of an ordered zone', async () => {
    const user = userEvent.setup();
    await openDragDropEditor(user);
    await fillBasics(user);
    await user.click(screen.getByRole('button', { name: 'Add zone' }));
    await replace(user, 'Zone 1: items it holds', '2');
    await user.click(screen.getByRole('checkbox', { name: 'Zone 1: order matters' }));
    await user.type(screen.getByLabelText('Item 1'), 'A');
    await user.selectOptions(screen.getByLabelText('Correct zone for item 1'), 'Zone 1');
    await user.click(screen.getByRole('button', { name: 'Add item' }));
    await user.type(screen.getByLabelText('Item 2'), 'B');
    await user.selectOptions(screen.getByLabelText('Correct zone for item 2'), 'Zone 1');

    await user.click(screen.getByRole('button', { name: 'Move A down' }));
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    await waitFor(() => {
      expect(created).toHaveLength(1);
    });
    expect(created[0]).toEqual(
      expect.objectContaining({ gradingSpec: { zones: [{ zoneId: 'z1', itemIds: ['i2', 'i1'], ordered: true }] } }),
    );
  });

  it('shows why a diagram upload was rejected', async () => {
    server.use(
      http.post('*/api/lessons/:lessonId/diagram-images', () =>
        HttpResponse.json({ code: 'QUESTION_DIAGRAM_IMAGE_TYPE_INVALID' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    await openDragDropEditor(user);

    await uploadDiagram(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Use a PNG, JPEG or WebP image.');
    expect(screen.queryByText(/Current image/)).not.toBeInTheDocument();
  });

  it('shows diagram errors on submit and sends nothing', async () => {
    const user = userEvent.setup();
    await openDragDropEditor(user);

    await user.click(screen.getByRole('button', { name: 'Create question' }));

    expect(await screen.findByText('Upload the diagram image.')).toBeInTheDocument();
    expect(screen.getByText('Draw at least one drop zone.')).toBeInTheDocument();
    expect(created).toEqual([]);
  });

  it('shows a server diagram error on the drop zones', async () => {
    server.use(
      http.post('*/api/questions', () =>
        HttpResponse.json({ code: 'QUESTION_DIAGRAM_ZONES_OVERLAP' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    await openDragDropEditor(user);
    await fillBasics(user);
    await drawZone(user);
    await user.type(screen.getByLabelText('Item 1'), 'Nucleus');
    await user.selectOptions(screen.getByLabelText('Correct zone for item 1'), 'Zone 1');

    await user.click(screen.getByRole('button', { name: 'Create question' }));

    const zones = within(screen.getByRole('group', { name: 'Drop zones' }));
    expect(await zones.findByText('Drop zones must not overlap.')).toBeInTheDocument();
  });

  it('previews the diagram as the student sees it and reveals the correct placements', async () => {
    const user = userEvent.setup();
    await openDragDropEditor(user);
    await fillBasics(user);
    await drawZone(user);
    await user.type(screen.getByLabelText('Item 1'), 'Nucleus');
    await user.selectOptions(screen.getByLabelText('Correct zone for item 1'), 'Zone 1');
    await user.click(screen.getByRole('button', { name: 'Add item' }));
    await user.type(screen.getByLabelText('Item 2'), 'Engine');
    const preview = within(screen.getByRole('region', { name: 'Student preview' }));

    expect(await preview.findByRole('img', { name: 'Plant cell' })).toBeInTheDocument();
    const bank = within(preview.getByRole('region', { name: 'Items to place' }));
    expect(bank.getAllByRole('listitem').map((item) => item.textContent)).toEqual(['Nucleus', 'Engine']);
    expect(preview.getByRole('button', { name: 'Try the answer' })).toBeInTheDocument();
    await user.click(preview.getByRole('checkbox', { name: 'Show correct placements' }));
    expect(preview.getByText('Zone 1: Nucleus')).toBeInTheDocument();
    expect(preview.getByText('Distractors (stay unplaced): Engine')).toBeInTheDocument();
  });

  it('grades a drag-and-drop answer with the test grader', async () => {
    let body: unknown = null;
    server.use(
      getGradeQuestionDraftMockHandler(async ({ request }) => {
        body = await request.json();
        return { score: 1, normalisedScore: 1, outcome: 'Correct', maxScore: 1, feedback: 'All items placed.' };
      }),
    );
    const user = userEvent.setup();
    await openDragDropEditor(user);
    await fillBasics(user);
    await drawZone(user);
    await user.type(screen.getByLabelText('Item 1'), 'Nucleus');
    await user.selectOptions(screen.getByLabelText('Correct zone for item 1'), 'Zone 1');
    const preview = within(screen.getByRole('region', { name: 'Student preview' }));

    await user.click(await preview.findByRole('button', { name: 'Nucleus' }));
    await user.click(preview.getByRole('button', { name: 'Place here (Nucleus in zone 1)' }));
    await user.click(preview.getByRole('button', { name: 'Try the answer' }));

    expect(await preview.findByText('All items placed.')).toBeInTheDocument();
    expect(body).toEqual(expect.objectContaining({ answer: { placements: [{ zoneId: 'z1', itemIds: ['i1'] }] } }));
  });

  it('has no axe violations', async () => {
    const user = userEvent.setup();
    const { container } = await openDragDropEditor(user);
    await fillBasics(user);
    await drawZone(user);

    expect((await axe(container)).violations).toEqual([]);
  });
});
