import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetSubjectExamBlueprintsMockHandler } from '@/shared/api/generated/exam-blueprints/exam-blueprints.msw';
import type { SubjectResult } from '@/shared/api/generated/model';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { axe } from '@/test/axe';
import { blueprintSubjectId, mathSubjectId, overview, servable } from '@/test/blueprintFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const subjects: SubjectResult[] = [
  { id: blueprintSubjectId, name: 'Physics', order: 1, unitCount: 1 },
  { id: mathSubjectId, name: 'Math', order: 2, unitCount: 1 },
];

const mathOverview = overview({
  subjectId: mathSubjectId,
  subjectName: 'Math',
  defaultBlueprint: null,
  units: [{ unitId: 'algebra', name: 'Algebra', order: 1, blueprint: null, servable: servable({}) }],
});

const openBlueprints = (list: SubjectResult[] = subjects, lng: 'en' | 'ar' = 'en') => {
  server.use(
    getGetSubjectsMockHandler(list),
    getGetSubjectExamBlueprintsMockHandler(({ params }) =>
      params.subjectId === mathSubjectId ? mathOverview : overview(),
    ),
  );
  return renderApp('/admin/blueprints', { session: testSessions.admin, lng });
};

describe('ExamBlueprintsPage', () => {
  it('shows the default and unit blueprints after loading', async () => {
    openBlueprints();

    expect(await screen.findByRole('status', { name: 'Loading exam blueprints' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Subject default blueprint' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Unit: Mechanics' })).toBeInTheDocument();
    const editor = screen.getByRole('region', { name: 'Subject default blueprint' });
    expect(within(within(editor).getByRole('row', { name: /Multiple choice/ })).getByText('3')).toBeInTheDocument();
    expect(within(within(editor).getByRole('row', { name: /Fill in the blank/ })).getByText('1')).toBeInTheDocument();
  });

  it('shows the empty state when there are no subjects', async () => {
    openBlueprints([]);

    expect(await screen.findByText('No subjects yet. Add one from the content page.')).toBeInTheDocument();
    const main = screen.getByRole('main');
    expect(within(main).getByRole('link', { name: 'Content' })).toHaveAttribute('href', '/admin/content');
  });

  it('shows the error state and retries', async () => {
    openBlueprints();
    server.use(
      http.get('*/api/subjects', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const user = userEvent.setup();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load subjects');

    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'Subject default blueprint' })).toBeInTheDocument();
  });

  it('switches subject from the select', async () => {
    const { router } = openBlueprints();
    const user = userEvent.setup();

    await user.selectOptions(await screen.findByLabelText('Subject'), 'Math');

    expect(await screen.findByRole('heading', { name: 'Unit: Algebra' })).toBeInTheDocument();
    expect(screen.getByText('No default blueprint saved yet.')).toBeInTheDocument();
    expect(router.state.location.search).toEqual({ subjectId: mathSubjectId });
  });

  it('renders right-to-left in Arabic', async () => {
    openBlueprints(subjects, 'ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'نماذج الامتحانات' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(await screen.findByRole('heading', { name: 'النموذج الافتراضي للمادة' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = openBlueprints();

    await screen.findByRole('heading', { name: 'Subject default blueprint' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
