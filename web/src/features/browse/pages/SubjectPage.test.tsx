import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetStudentSubjectMockHandler } from '@/shared/api/generated/browse/browse.msw';
import { axe } from '@/test/axe';
import { browseSecondUnitId, browseSubjectId, browseUnitId, studentSubject } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openSubject = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/subject/${browseSubjectId}`, { session: testSessions.student, lng });

describe('SubjectPage', () => {
  it('shows a loading state then the subject mastery and its units in order', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    openSubject();

    expect(await screen.findByRole('status', { name: 'Loading the subject…' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { level: 1, name: 'Physics' })).toBeInTheDocument();
    expect(screen.getByText('40% mastered · 10 questions available · Seen 6')).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /^(Mechanics|Waves)$/ }).map((link) => link.textContent)).toEqual([
      'Mechanics',
      'Waves',
    ]);
  });

  it('numbers the units in order', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    openSubject();

    await screen.findByRole('heading', { level: 1, name: 'Physics' });

    expect(screen.getAllByText(/^[12]$/).map((el) => el.textContent)).toEqual(['1', '2']);
  });

  it('links each unit to its unit page and its exam start', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    openSubject();

    expect(await screen.findByRole('link', { name: 'Mechanics' })).toHaveAttribute(
      'href',
      `/student/unit/${browseUnitId}`,
    );
    expect(screen.getByRole('link', { name: 'Unit exam: Mechanics' })).toHaveAttribute(
      'href',
      `/student/exam-start/${browseUnitId}`,
    );
    expect(screen.getByRole('link', { name: 'Unit exam: Waves' })).toHaveAttribute(
      'href',
      `/student/exam-start/${browseSecondUnitId}`,
    );
  });

  it('shows the best exam score or a dash per unit', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    openSubject();

    expect(await screen.findByText('Best exam score: 72 / 100')).toBeInTheDocument();
    expect(screen.getByText('Best exam score: —')).toBeInTheDocument();
    expect(screen.getByText('50% mastered · 2 lessons')).toBeInTheDocument();
  });

  it('links to the multi-unit exam builder for this subject', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    openSubject();

    await screen.findByRole('heading', { level: 1, name: 'Physics' });
    const link = within(screen.getByRole('main')).getByRole('link', { name: 'Multi-unit exam' });
    expect(link.getAttribute('href')).toContain('/student/multi-exam');
    expect(link.getAttribute('href')).toContain(browseSubjectId);
  });

  it('shows breadcrumbs back to home', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    openSubject();

    const breadcrumb = await screen.findByRole('navigation', { name: 'Breadcrumb' });
    expect(within(breadcrumb).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/student');
    expect(within(breadcrumb).getByText('Physics')).toHaveAttribute('aria-current', 'page');
  });

  it('shows the empty state when the subject has no units', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject({ units: [] })));
    openSubject();

    expect(await screen.findByText('This subject has no units yet.')).toBeInTheDocument();
  });

  it('shows the error state and retries', async () => {
    server.use(
      http.get('*/api/browse/subjects/:subjectId', () =>
        HttpResponse.json({ code: 'SUBJECT_NOT_FOUND' }, { status: 404 }),
      ),
    );
    const user = userEvent.setup();
    openSubject();

    expect(await screen.findByRole('alert')).toHaveTextContent('Subject not found.');
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Physics' })).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    openSubject('ar');

    await screen.findByRole('heading', { level: 1, name: 'Physics' });
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'امتحان متعدد الوحدات' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    server.use(getGetStudentSubjectMockHandler(studentSubject()));
    const { container } = openSubject();

    await screen.findByRole('heading', { level: 1, name: 'Physics' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
