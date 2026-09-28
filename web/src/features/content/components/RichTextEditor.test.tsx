import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import {
  getGetLessonMockHandler,
  getUpdateLessonMockHandler,
  getUploadLessonImageMockHandler,
} from '@/shared/api/generated/lessons/lessons.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lesson: LessonDetailResult = {
  id: 'l1',
  unitId: 'u1',
  name: "Newton's laws",
  order: 1,
  state: 'Draft',
  explanation: '<p>A force changes motion <span data-type="inline-math" data-latex="F=ma"></span></p>',
  summary: '',
  videoUrl: null,
  objectives: [],
};

const diagram = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'diagram.png', { type: 'image/png' });

const openEditor = async (user: ReturnType<typeof userEvent.setup>, tool: string) => {
  renderApp('/admin/lesson/l1', { session: testSessions.admin });
  const toolbar = await screen.findByRole('toolbar', { name: 'Explanation formatting' });
  await user.click(within(toolbar).getByRole('button', { name: tool }));
  return within(await screen.findByRole('dialog', { name: tool }));
};

const findPreview = async () => within(await screen.findByRole('region', { name: 'Preview' }));

describe('RichTextEditor', () => {
  let bodies: { explanation?: string }[];
  let uploads: string[];

  beforeEach(() => {
    bodies = [];
    uploads = [];
    server.use(
      getGetLessonMockHandler(lesson),
      getUpdateLessonMockHandler(async ({ request }) => {
        bodies.push((await request.json()) as { explanation?: string });
      }),
      getUploadLessonImageMockHandler(({ request }) => {
        uploads.push(new URL(request.url).pathname);
        return { url: '/api/media/lessons/l1/a.png' };
      }),
    );
  });

  it('inserts an inline formula without saving the lesson', async () => {
    const user = userEvent.setup();
    const dialog = await openEditor(user, 'Insert formula');

    await user.type(dialog.getByLabelText('LaTeX'), 'a^2+b^2');
    await user.click(dialog.getByRole('button', { name: 'Insert' }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(bodies).toEqual([]);
    await waitFor(async () => {
      expect((await findPreview()).getAllByRole('math', { hidden: true })).toHaveLength(2);
    });
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.explanation).toContain('data-latex="a^2+b^2"');
  });

  it('inserts a block formula', async () => {
    const user = userEvent.setup();
    const dialog = await openEditor(user, 'Insert formula');

    await user.type(dialog.getByLabelText('LaTeX'), 'E=mc^2');
    await user.click(dialog.getByLabelText('Show on its own line'));
    await user.click(dialog.getByRole('button', { name: 'Insert' }));

    await waitFor(() => {
      expect(screen.getByRole('region', { name: 'Preview' }).querySelector('.katex-display')).not.toBeNull();
    });
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.explanation).toContain('data-type="block-math"');
    expect(bodies[0]?.explanation).toContain('data-latex="E=mc^2"');
  });

  it('applies bold from the toolbar', async () => {
    const user = userEvent.setup();
    renderApp('/admin/lesson/l1', { session: testSessions.admin });
    const toolbar = within(await screen.findByRole('toolbar', { name: 'Explanation formatting' }));
    const bold = toolbar.getByRole('button', { name: 'Bold' });

    await user.click(screen.getByRole('textbox', { name: 'Explanation' }));
    await user.keyboard('{Control>}a{/Control}');
    await user.click(bold);

    await waitFor(() => {
      expect(bold).toHaveAttribute('aria-pressed', 'true');
    });
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.explanation).toContain('<strong>A force changes motion');
  });

  it('keeps the formula dialog open on empty LaTeX', async () => {
    const user = userEvent.setup();
    const dialog = await openEditor(user, 'Insert formula');

    await user.click(dialog.getByRole('button', { name: 'Insert' }));

    expect(await dialog.findByText('This field is required.')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Insert formula' })).toBeInTheDocument();
  });

  it('uploads an image and inserts it with its description', async () => {
    const user = userEvent.setup();
    const dialog = await openEditor(user, 'Insert image');

    await user.upload(dialog.getByLabelText('Image file'), diagram);
    await user.type(dialog.getByLabelText('Image description'), 'Force diagram');
    await user.click(dialog.getByRole('button', { name: 'Upload and insert' }));

    expect(await screen.findByText('Image inserted.')).toBeInTheDocument();
    expect(uploads).toEqual(['/api/lessons/l1/images']);
    const image = await (await findPreview()).findByRole('img', { name: 'Force diagram' });
    expect(image).toHaveAttribute('src', '/api/media/lessons/l1/a.png');
  });

  it('shows the upload error inside the image dialog', async () => {
    server.use(
      http.post('*/api/lessons/:lessonId/images', () =>
        HttpResponse.json({ code: 'LESSON_IMAGE_TOO_LARGE' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    const dialog = await openEditor(user, 'Insert image');

    await user.upload(dialog.getByLabelText('Image file'), diagram);
    await user.type(dialog.getByLabelText('Image description'), 'Force diagram');
    await user.click(dialog.getByRole('button', { name: 'Upload and insert' }));

    expect(await dialog.findByText('The image is too large.')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Insert image' })).toBeInTheDocument();
  });
});
