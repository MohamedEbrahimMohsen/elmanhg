import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import { getGetLessonMockHandler, getUpdateLessonMockHandler } from '@/shared/api/generated/lessons/lessons.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const explanation = '<p>A force changes motion <span data-type="inline-math" data-latex="F=ma"></span></p>';

const lesson: LessonDetailResult = {
  id: 'l1',
  unitId: 'u1',
  name: "Newton's laws",
  order: 1,
  state: 'Draft',
  explanation,
  summary: '<p>Forces cause acceleration.</p>',
  videoUrl: 'https://example.com/video',
  objectives: [
    { id: 'o2', text: 'Apply F = ma', order: 2 },
    { id: 'o1', text: 'State the first law', order: 1 },
  ],
};

const openEditor = (lng: 'en' | 'ar' = 'en') => renderApp('/admin/lesson/l1', { session: testSessions.admin, lng });

const findPreview = async () => within(await screen.findByRole('region', { name: 'Preview' }));

describe('LessonEditorPage', () => {
  let bodies: unknown[];

  beforeEach(() => {
    bodies = [];
    server.use(
      getGetLessonMockHandler(lesson),
      getUpdateLessonMockHandler(async ({ request }) => {
        bodies.push(await request.json());
      }),
    );
  });

  it('shows the lesson after loading', async () => {
    openEditor();

    expect(await screen.findByRole('status', { name: 'Loading lesson' })).toBeInTheDocument();
    expect(await screen.findByLabelText('Name')).toHaveValue("Newton's laws");
    const video = screen.getByLabelText('Video link (optional)');
    expect(video).toHaveValue('https://example.com/video');
    expect(video).toHaveAttribute('dir', 'ltr');
    expect(screen.getByLabelText('Objective 1')).toHaveValue('State the first law');
    expect(screen.getByLabelText('Objective 2')).toHaveValue('Apply F = ma');
    expect(screen.getAllByText('Draft')[0]).toBeInTheDocument();
    expect((await findPreview()).getByText(/A force changes motion/)).toBeInTheDocument();
  });

  it('shows an error and recovers on retry', async () => {
    server.use(
      http.get('*/api/lessons/:lessonId', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    openEditor();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the lesson');
    server.use(getGetLessonMockHandler(lesson));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByLabelText('Name')).toBeInTheDocument();
  });

  it('shows that the lesson was not found', async () => {
    server.use(
      http.get('*/api/lessons/:lessonId', () => HttpResponse.json({ code: 'LESSON_NOT_FOUND' }, { status: 404 })),
    );
    openEditor();

    expect(await screen.findByRole('alert')).toHaveTextContent('Lesson not found.');
  });

  it('saves the edited lesson', async () => {
    const user = userEvent.setup();
    openEditor();

    const name = await screen.findByLabelText('Name');
    await user.clear(name);
    await user.type(name, 'Laws of motion');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Lesson saved.')).toBeInTheDocument();
    expect(bodies).toEqual([
      {
        name: 'Laws of motion',
        explanation,
        summary: '<p>Forces cause acceleration.</p>',
        videoUrl: 'https://example.com/video',
        objectives: [
          { id: 'o1', text: 'State the first law' },
          { id: 'o2', text: 'Apply F = ma' },
        ],
      },
    ]);
  });

  it('edits the objectives list', async () => {
    const user = userEvent.setup();
    openEditor();

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('button', { name: 'Add objective' }));
    await user.type(screen.getByLabelText('Objective 3'), 'Explain inertia');
    await user.click(screen.getByRole('button', { name: 'Move objective 3 up' }));
    await user.click(screen.getByRole('button', { name: 'Remove objective 1' }));
    const preview = await findPreview();
    const previewItems = preview.getAllByRole('listitem').map((item) => item.textContent);
    expect(previewItems).toEqual(['Explain inertia', 'Apply F = ma']);
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]).toMatchObject({
      objectives: [
        { id: null, text: 'Explain inertia' },
        { id: 'o2', text: 'Apply F = ma' },
      ],
    });
  });

  it('moves an objective down', async () => {
    const user = userEvent.setup();
    openEditor();

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('button', { name: 'Move objective 1 down' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]).toMatchObject({
      objectives: [
        { id: 'o2', text: 'Apply F = ma' },
        { id: 'o1', text: 'State the first law' },
      ],
    });
  });

  it('shows required errors without calling the API', async () => {
    const user = userEvent.setup();
    openEditor();

    await user.clear(await screen.findByLabelText('Name'));
    await user.clear(screen.getByLabelText('Objective 1'));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findAllByText('This field is required.')).toHaveLength(2);
    expect(bodies).toEqual([]);
  });

  it('shows a server error under the video link', async () => {
    server.use(
      http.put('*/api/lessons/:lessonId', () =>
        HttpResponse.json({ code: 'LESSON_VIDEO_URL_INVALID' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Video link (optional)')).toHaveAccessibleDescription(
        'Enter a video link that starts with http:// or https://.',
      );
    });
  });

  it('shows a server error under the explanation', async () => {
    server.use(
      http.put('*/api/lessons/:lessonId', () =>
        HttpResponse.json({ code: 'LESSON_EXPLANATION_TOO_LONG' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(screen.getByRole('textbox', { name: 'Explanation' })).toHaveAccessibleDescription(
        'The explanation is too long.',
      );
    });
  });

  it('shows a server error under the objectives', async () => {
    server.use(
      http.put('*/api/lessons/:lessonId', () =>
        HttpResponse.json({ code: 'LESSON_OBJECTIVES_TOO_MANY' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    const objectives = screen.getByRole('group', { name: 'Objectives' });
    expect(await within(objectives).findByText('This lesson has too many objectives.')).toBeInTheDocument();
  });

  it('renders LaTeX from the explanation in the preview', async () => {
    openEditor();

    expect((await findPreview()).getAllByRole('math', { hidden: true })).toHaveLength(1);
  });

  it('links to a new question and to the lesson questions', async () => {
    openEditor();

    expect(await screen.findByRole('link', { name: 'New question' })).toHaveAttribute('href', '/admin/question/new/l1');
    expect(screen.getByRole('link', { name: 'Lesson questions' })).toHaveAttribute(
      'href',
      '/admin/questions?lessonId=l1',
    );
  });

  it('links to the question import page', async () => {
    openEditor();

    expect(await screen.findByRole('link', { name: 'Import questions' })).toHaveAttribute(
      'href',
      '/admin/question/import/l1',
    );
  });

  it('renders right-to-left in Arabic', async () => {
    openEditor('ar');

    expect(await screen.findByRole('heading', { name: 'تحرير الدرس' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openEditor();

    await screen.findByLabelText('Name');

    expect((await axe(container)).violations).toEqual([]);
  });
});
