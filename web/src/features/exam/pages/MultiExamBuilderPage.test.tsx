import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Language } from '@/app/i18n';
import {
  getGetMultiUnitExamOverviewMockHandler,
  getPreviewMultiUnitExamMockHandler,
  getStartMultiUnitExamMockHandler,
} from '@/shared/api/generated/exams/exams.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { axe } from '@/test/axe';
import {
  examItem,
  examSecondUnitId,
  examSessionId,
  examSubjectId,
  examUnitId,
  multiOverview,
  multiPreview,
  openExam,
} from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const chemistryId = '67676767-6767-4676-8676-676767676767';
const subjects = [
  { id: examSubjectId, name: 'Physics', order: 1, unitCount: 2 },
  { id: chemistryId, name: 'Chemistry', order: 2, unitCount: 2 },
];

const previewForSize = getPreviewMultiUnitExamMockHandler(({ request }) => {
  const size = Number(new URL(request.url).searchParams.get('size'));
  return multiPreview({
    size,
    blueprint: { ...multiPreview().blueprint, typeCounts: [{ type: 'Mcq', required: size, available: 25 }] },
  });
});

async function openBuilder(search = '', lng: Language = 'en') {
  const rendered = renderApp(`/student/multi-exam${search}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/multi-exam']);
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/exam/$sessionId']);
  return rendered;
}

async function tickBoth(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('checkbox', { name: 'Mechanics' }));
  await user.click(screen.getByRole('checkbox', { name: 'Waves' }));
}

const withSelection = `?subjectId=${examSubjectId}&unitIds=${encodeURIComponent(JSON.stringify([examUnitId, examSecondUnitId]))}`;

describe('MultiExamBuilderPage', () => {
  it('shows the units of the first subject after loading', async () => {
    server.use(getGetSubjectsMockHandler(subjects), getGetMultiUnitExamOverviewMockHandler(multiOverview()));
    const rendered = openBuilder();

    expect(await screen.findByRole('status', { name: 'Loading units…' })).toBeInTheDocument();
    await rendered;
    expect(await screen.findByRole('checkbox', { name: 'Mechanics' })).toBeInTheDocument();
    expect(screen.getByText('(12 questions available)')).toBeInTheDocument();
  });

  it('asks for two units when fewer are selected', async () => {
    server.use(getGetSubjectsMockHandler(subjects), getGetMultiUnitExamOverviewMockHandler(multiOverview()));
    const user = userEvent.setup();
    await openBuilder();

    await user.click(await screen.findByRole('checkbox', { name: 'Mechanics' }));

    expect(await screen.findByText('Choose at least two units.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Merged blueprint (proportional)' })).toBeNull();
  });

  it('shows the merged blueprint when two units are selected', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      getPreviewMultiUnitExamMockHandler(multiPreview()),
    );
    const user = userEvent.setup();
    await openBuilder();

    await tickBoth(user);

    const row = await screen.findByRole('row', { name: /Multiple choice/ });
    expect(
      within(row)
        .getAllByRole('cell')
        .map((cell) => cell.textContent),
    ).toEqual(['Multiple choice', '20', '25']);
    expect(screen.getByText('Time: 47 min')).toBeInTheDocument();
    expect(screen.getByText('Pass mark: 57')).toBeInTheDocument();
    expect(screen.getByText('Mechanics: 10 questions')).toBeInTheDocument();
  });

  it('updates the preview when another size is chosen', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      previewForSize,
    );
    const user = userEvent.setup();
    await openBuilder();
    await tickBoth(user);
    await screen.findByRole('row', { name: /Multiple choice/ });

    await user.click(screen.getByRole('radio', { name: '40 questions' }));

    await waitFor(() => {
      expect(within(screen.getByRole('row', { name: /Multiple choice/ })).getAllByRole('cell')[1]).toHaveTextContent(
        '40',
      );
    });
  });

  it('shows the shortfall and hides Start when questions are short', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      getPreviewMultiUnitExamMockHandler(multiPreview({ isAvailable: false })),
    );
    const user = userEvent.setup();
    await openBuilder();

    await tickBoth(user);

    expect(
      await screen.findByText('The exam cannot be created: not enough questions are available.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start exam' })).toBeNull();
  });

  it('disables a unit without an exam blueprint', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(
        multiOverview({
          units: [
            { unitId: examUnitId, name: 'Mechanics', hasBlueprint: true, isSubjectDefault: false, servableCount: 12 },
            { unitId: examSecondUnitId, name: 'Waves', hasBlueprint: false, isSubjectDefault: false, servableCount: 0 },
          ],
        }),
      ),
    );
    await openBuilder();

    expect(await screen.findByRole('checkbox', { name: 'Waves' })).toBeDisabled();
    expect(screen.getByText('This unit has no exam')).toBeInTheDocument();
  });

  it('starts the exam and opens the exam screen', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      getPreviewMultiUnitExamMockHandler(multiPreview()),
      getStartMultiUnitExamMockHandler(
        openExam([examItem(1)], {
          kind: 'MultiUnitExam',
          units: [
            { unitId: examUnitId, name: 'Mechanics' },
            { unitId: examSecondUnitId, name: 'Waves' },
          ],
        }),
      ),
    );
    const user = userEvent.setup();
    await openBuilder();
    await tickBoth(user);

    await user.click(await screen.findByRole('button', { name: 'Start exam' }));

    expect(await screen.findByRole('heading', { name: 'Multi-unit exam: Mechanics + Waves' })).toBeInTheDocument();
  });

  it('shows the server error when the start is refused', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      getPreviewMultiUnitExamMockHandler(multiPreview()),
      http.post('*/api/exams/subjects/:subjectId/multi-unit', () =>
        HttpResponse.json({ code: 'EXAM_ALREADY_IN_PROGRESS' }, { status: 409 }),
      ),
    );
    const user = userEvent.setup();
    await openBuilder();
    await tickBoth(user);

    await user.click(await screen.findByRole('button', { name: 'Start exam' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'You already have an exam in progress. Finish it first.',
    );
  });

  it('warns about an exam in progress and links to it', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(
        multiOverview({ inProgressExam: { sessionId: examSessionId, isThisUnit: false } }),
      ),
      getPreviewMultiUnitExamMockHandler(multiPreview()),
    );
    const user = userEvent.setup();
    await openBuilder();
    await tickBoth(user);

    expect(await screen.findByRole('heading', { name: 'Merged blueprint (proportional)' })).toBeInTheDocument();
    expect(screen.getByText('You have an exam in progress.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Continue that exam' })).toHaveAttribute(
      'href',
      `/student/exam/${examSessionId}`,
    );
    expect(screen.queryByRole('button', { name: 'Start exam' })).toBeNull();
  });

  it('clears the selection when the subject changes', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      getPreviewMultiUnitExamMockHandler(multiPreview()),
    );
    const user = userEvent.setup();
    await openBuilder(withSelection);
    expect(await screen.findByRole('checkbox', { name: 'Mechanics' })).toBeChecked();

    await user.selectOptions(screen.getByRole('combobox', { name: 'Subject' }), 'Chemistry');

    await waitFor(() => {
      expect(screen.getByRole('combobox', { name: 'Subject' })).toHaveValue(chemistryId);
      expect(screen.getByRole('checkbox', { name: 'Mechanics' })).not.toBeChecked();
      expect(screen.getByRole('checkbox', { name: 'Waves' })).not.toBeChecked();
    });
  });

  it('restores the selection from the URL', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview()),
      previewForSize,
    );
    await openBuilder(`${withSelection}&size=40`);

    expect(await screen.findByRole('checkbox', { name: 'Mechanics' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Waves' })).toBeChecked();
    expect(screen.getByRole('radio', { name: '40 questions' })).toBeChecked();
  });

  it('shows retry when the units fail to load', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      http.get('*/api/exams/subjects/:subjectId/multi-unit', () =>
        HttpResponse.json({ title: 'Boom' }, { status: 500 }),
      ),
    );
    const user = userEvent.setup();
    await openBuilder();

    const retry = await screen.findByRole('button', { name: 'Retry' });
    expect(screen.getByText("Couldn't load the units.")).toBeInTheDocument();
    server.use(getGetMultiUnitExamOverviewMockHandler(multiOverview()));
    await user.click(retry);

    expect(await screen.findByRole('checkbox', { name: 'Mechanics' })).toBeInTheDocument();
  });

  it('says the subject has no units', async () => {
    server.use(
      getGetSubjectsMockHandler(subjects),
      getGetMultiUnitExamOverviewMockHandler(multiOverview({ units: [] })),
    );
    await openBuilder();

    expect(await screen.findByText('This subject has no units yet.')).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic without axe violations', async () => {
    server.use(getGetSubjectsMockHandler(subjects), getGetMultiUnitExamOverviewMockHandler(multiOverview()));
    const { container } = await openBuilder('', 'ar');

    expect(await screen.findByRole('heading', { name: 'امتحان متعدد الوحدات' })).toBeInTheDocument();
    await screen.findByRole('checkbox', { name: 'Mechanics' });
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect((await axe(container)).violations).toEqual([]);
  });
});
