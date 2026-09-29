import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Language } from '@/app/i18n';
import type { UsageResult } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import {
  getCreateTeacherThreadMockHandler,
  getGetMyTeacherThreadMockHandler,
  getGetTeacherThreadContextMockHandler,
} from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import {
  askTeacherUsage,
  attemptId,
  contextLessonId,
  teacherThread,
  threadContext,
  threadId,
} from '@/test/askTeacherFixtures';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { baseUsage, planCatalogue } from '@/test/subscriptionFixtures';

const attemptStem = '<p>Why does the ball fall?</p>';
const imageUrl = '*/api/media/teacher-threads/:file';

const fieldText = (value: FormDataEntryValue | null) => (typeof value === 'string' ? value : null);

async function openNew(
  search: string,
  {
    usage = askTeacherUsage(),
    lng = 'en',
    failContext = false,
  }: { usage?: UsageResult; lng?: Language; failContext?: boolean } = {},
) {
  server.use(
    getGetMyUsageMockHandler(usage),
    getGetPlanCatalogueMockHandler(planCatalogue()),
    getGetTeacherThreadContextMockHandler(
      threadContext(search.includes('attemptId') ? { questionStem: attemptStem, attemptId } : {}),
    ),
    getGetMyTeacherThreadMockHandler(teacherThread()),
    http.get(
      imageUrl,
      () => new HttpResponse(new Uint8Array([0x89, 0x50, 0x4e, 0x47]), { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  if (failContext) {
    server.use(
      http.get(
        '*/api/teacher-threads/context',
        () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
        {
          once: true,
        },
      ),
    );
  }
  const rendered = renderApp(`/student/ask-new${search}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/ask-new']);
  return rendered;
}

const questionField = () => screen.findByLabelText('Your question');
const sendButton = () => screen.getByRole('button', { name: 'Send' });

describe('AskTeacherNewPage', () => {
  it('shows the attached lesson context from a lesson link', async () => {
    await openNew(`?lessonId=${contextLessonId}`);

    expect(await screen.findByText('Attached automatically')).toBeInTheDocument();
    expect(screen.getByText("Physics / Mechanics / Newton's laws")).toBeInTheDocument();
  });

  it('shows the question stem for an attempt context', async () => {
    await openNew(`?attemptId=${attemptId}`);

    expect(await screen.findByText('Question:')).toBeInTheDocument();
    expect(screen.getByText('Why does the ball fall?')).toBeInTheDocument();
  });

  it('sends the question with the attempt and photo, then opens the thread', async () => {
    let sent: FormData | null = null;
    const user = userEvent.setup();
    const { router } = await openNew(`?attemptId=${attemptId}`);
    server.use(
      getCreateTeacherThreadMockHandler(async ({ request }) => {
        sent = await request.formData();
        return teacherThread();
      }),
    );

    await user.type(await questionField(), 'Why is my answer wrong?');
    await user.upload(
      screen.getByLabelText('Attach a photo of your work (optional)'),
      new File([new Uint8Array([0x89])], 'photo.png', { type: 'image/png' }),
    );
    await screen.findByText("Physics / Mechanics / Newton's laws");
    await user.click(sendButton());

    expect(await screen.findByText('Your question was sent.')).toBeInTheDocument();
    await expect.poll(() => router.state.location.pathname).toBe(`/student/thread/${threadId}`);
    const body = sent as FormData | null;
    expect(fieldText(body?.get('text') ?? null)).toBe('Why is my answer wrong?');
    expect(fieldText(body?.get('attemptId') ?? null)).toBe(attemptId);
    expect(body?.has('lessonId')).toBe(false);
    expect((body?.get('image') as File | null)?.name).toBe('photo.png');
    expect(await screen.findByText('Why is F = ma?')).toBeInTheDocument();
  });

  it('shows the required error when the question is blank', async () => {
    let requests = 0;
    const user = userEvent.setup();
    await openNew(`?lessonId=${contextLessonId}`);
    server.use(
      getCreateTeacherThreadMockHandler(() => {
        requests += 1;
        return teacherThread();
      }),
    );

    await questionField();
    await user.click(sendButton());

    expect(await screen.findByText('Write your question')).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('shows the server image error under the photo field', async () => {
    const user = userEvent.setup();
    await openNew(`?lessonId=${contextLessonId}`);
    server.use(
      http.post('*/api/teacher-threads', () =>
        HttpResponse.json({ code: 'TEACHER_THREAD_IMAGE_TYPE_INVALID' }, { status: 422 }),
      ),
    );

    await user.type(await questionField(), 'Why?');
    await user.click(sendButton());

    expect(await screen.findByText('The image must be PNG, JPG or WEBP.')).toBeInTheDocument();
    expect(screen.getByLabelText('Attach a photo of your work (optional)')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows the monthly limit error as a form alert', async () => {
    const user = userEvent.setup();
    await openNew(`?lessonId=${contextLessonId}`);
    server.use(
      http.post('*/api/teacher-threads', () =>
        HttpResponse.json({ code: 'ASK_TEACHER_MONTHLY_LIMIT_REACHED' }, { status: 403 }),
      ),
    );

    await user.type(await questionField(), 'Why?');
    await user.click(sendButton());

    expect(await screen.findByRole('alert')).toHaveTextContent('You have used all your questions for this month.');
  });

  it('shows the used-quota notice instead of the form', async () => {
    await openNew(`?lessonId=${contextLessonId}`, {
      usage: askTeacherUsage({ askTeacherQuestionsUsedThisMonth: 20, askTeacherQuestionsRemainingThisMonth: 0 }),
    });

    expect(await screen.findByText('You have used all 20 questions for this month.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
  });

  it('shows the upsell without the add-on', async () => {
    await openNew(`?lessonId=${contextLessonId}`, { usage: baseUsage() });

    expect(await screen.findByText('This is a paid add-on and needs the Base plan.')).toBeInTheDocument();
  });

  it('shows the context error state and retries', async () => {
    const user = userEvent.setup();
    await openNew(`?lessonId=${contextLessonId}`, { failContext: true });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load the context')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText("Physics / Mechanics / Newton's laws")).toBeInTheDocument();
  });

  it('shows the reply-time note from the plan catalogue', async () => {
    await openNew(`?lessonId=${contextLessonId}`);

    expect(
      await screen.findByText("Your question goes to the subject's teachers, who reply within 24 hours."),
    ).toBeInTheDocument();
  });

  it('disables Send while submitting', async () => {
    const user = userEvent.setup();
    await openNew(`?lessonId=${contextLessonId}`);
    server.use(
      getCreateTeacherThreadMockHandler(async () => {
        await delay('infinite');
        return teacherThread();
      }),
    );

    await user.type(await questionField(), 'Why?');
    await user.click(sendButton());

    await expect.poll(() => sendButton()).toBeDisabled();
    expect(sendButton()).toHaveAttribute('aria-busy', 'true');
  });

  it('renders right to left in Arabic', async () => {
    await openNew(`?lessonId=${contextLessonId}`, { lng: 'ar' });

    expect(await screen.findByRole('heading', { level: 1, name: 'سؤال جديد للمعلّم' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = await openNew(`?lessonId=${contextLessonId}`);

    await screen.findByText("Physics / Mechanics / Newton's laws");
    expect((await axe(container)).violations).toEqual([]);
  });
});
