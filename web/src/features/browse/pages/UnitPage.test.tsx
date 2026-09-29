import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetStudentUnitMockHandler } from '@/shared/api/generated/browse/browse.msw';
import { axe } from '@/test/axe';
import { browseLessonId, browseSubjectId, browseUnitId, studentUnit } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openUnit = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/unit/${browseUnitId}`, { session: testSessions.student, lng });

describe('UnitPage', () => {
  it('shows a loading state then the lessons in order with their mastery', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit()));
    openUnit();

    expect(await screen.findByRole('status', { name: 'Loading the unit…' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { level: 1, name: 'Mechanics' })).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /^(Forces|Energy)$/ }).map((link) => link.textContent)).toEqual([
      'Forces',
      'Energy',
    ]);
    expect(screen.getByText('50% mastered · 4 questions')).toBeInTheDocument();
  });

  it('links each lesson to its lesson page', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit()));
    openUnit();

    expect(await screen.findByRole('link', { name: 'Energy' })).toHaveAttribute(
      'href',
      `/student/lesson/${browseLessonId}`,
    );
  });

  it('shows breadcrumbs to home and the subject', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit()));
    openUnit();

    const breadcrumb = await screen.findByRole('navigation', { name: 'Breadcrumb' });
    expect(within(breadcrumb).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/student');
    expect(within(breadcrumb).getByRole('link', { name: 'Physics' })).toHaveAttribute(
      'href',
      `/student/subject/${browseSubjectId}`,
    );
    expect(within(breadcrumb).getByText('Mechanics')).toHaveAttribute('aria-current', 'page');
  });

  it('shows the best unit exam score and links to the exam start', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit()));
    openUnit();

    expect(await screen.findByText('Best score: 72 / 100')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the unit exam' })).toHaveAttribute(
      'href',
      `/student/exam-start/${browseUnitId}`,
    );
  });

  it('says the exam was not taken when there is no score', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit({ bestExamScorePercent: null })));
    openUnit();

    expect(await screen.findByText('You have not taken this exam yet.')).toBeInTheDocument();
  });

  it('shows the empty state when the unit has no lessons', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit({ lessons: [] })));
    openUnit();

    expect(await screen.findByText('This unit has no lessons yet.')).toBeInTheDocument();
  });

  it('shows the error state and retries', async () => {
    server.use(
      http.get('*/api/browse/units/:unitId', () => HttpResponse.json({ code: 'UNIT_NOT_FOUND' }, { status: 404 })),
    );
    const user = userEvent.setup();
    openUnit();

    expect(await screen.findByRole('alert')).toHaveTextContent('Unit not found.');
    server.use(getGetStudentUnitMockHandler(studentUnit()));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Mechanics' })).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit()));
    openUnit('ar');

    expect(await screen.findByRole('heading', { name: 'الدروس' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    server.use(getGetStudentUnitMockHandler(studentUnit()));
    const { container } = openUnit();

    await screen.findByRole('heading', { level: 1, name: 'Mechanics' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
