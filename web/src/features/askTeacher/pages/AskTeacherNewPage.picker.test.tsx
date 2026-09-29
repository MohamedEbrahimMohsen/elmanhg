import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getGetMasteryOverviewMockHandler,
  getGetSubjectMasteryMockHandler,
} from '@/shared/api/generated/mastery/mastery.msw';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import {
  getCreateTeacherThreadMockHandler,
  getGetMyTeacherThreadMockHandler,
} from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { askTeacherUsage, secondUnitLessonId, subjectMasteryDetail, teacherThread } from '@/test/askTeacherFixtures';
import { masteryOverview, physicsId } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { planCatalogue } from '@/test/subscriptionFixtures';

async function openPicker(onCreate: (body: FormData) => void = () => undefined) {
  server.use(
    getGetMyUsageMockHandler(askTeacherUsage()),
    getGetPlanCatalogueMockHandler(planCatalogue()),
    getGetMasteryOverviewMockHandler(masteryOverview()),
    getGetSubjectMasteryMockHandler(subjectMasteryDetail()),
    getCreateTeacherThreadMockHandler(async ({ request }) => {
      onCreate(await request.formData());
      return teacherThread();
    }),
    getGetMyTeacherThreadMockHandler(teacherThread()),
    http.get(
      '*/api/media/teacher-threads/:file',
      () => new HttpResponse(new Uint8Array([0x89]), { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  const rendered = renderApp('/student/ask-new', { session: testSessions.student });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/ask-new']);
  return rendered;
}

const subjectSelect = () => screen.findByRole('combobox', { name: 'Subject' });
const lessonSelect = () => screen.getByRole('combobox', { name: 'Lesson' });

describe('AskTeacherNewPage lesson picker', () => {
  it("lists the subjects and then the chosen subject's lessons grouped by unit", async () => {
    const user = userEvent.setup();
    await openPicker();

    const subjects = await subjectSelect();
    expect(within(subjects).getByRole('option', { name: 'Physics' })).toBeInTheDocument();
    expect(lessonSelect()).toBeDisabled();
    await user.selectOptions(subjects, physicsId);

    await expect.poll(() => lessonSelect()).toBeEnabled();
    const groups = within(lessonSelect()).getAllByRole('group');
    expect(groups.map((group) => group.getAttribute('label'))).toEqual(['Mechanics', 'Waves']);
    expect(
      within(within(lessonSelect()).getByRole('group', { name: 'Waves' })).getByRole('option', { name: 'Sound' }),
    ).toBeInTheDocument();
  });

  it('requires a lesson before sending', async () => {
    let requests = 0;
    const user = userEvent.setup();
    await openPicker(() => {
      requests += 1;
    });

    await user.type(await screen.findByLabelText('Your question'), 'Why?');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    const error = await screen.findByText('Choose a lesson', { selector: 'p' });
    expect(lessonSelect()).toHaveAttribute('aria-invalid', 'true');
    expect(lessonSelect()).toHaveAttribute('aria-describedby', error.id);
    expect(requests).toBe(0);
  });

  it('sends the picked lesson id', async () => {
    let lessonId: FormDataEntryValue | null = null;
    const user = userEvent.setup();
    const { router } = await openPicker((body) => {
      lessonId = body.get('lessonId');
    });

    await user.selectOptions(await subjectSelect(), physicsId);
    await expect.poll(() => lessonSelect()).toBeEnabled();
    await user.selectOptions(lessonSelect(), secondUnitLessonId);
    await user.type(screen.getByLabelText('Your question'), 'What is sound?');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    await expect.poll(() => router.state.location.pathname).toMatch(/^\/student\/thread\//);
    expect(lessonId).toBe(secondUnitLessonId);
  });
});
