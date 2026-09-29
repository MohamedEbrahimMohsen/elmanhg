import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetStudentUnitMockHandler } from '@/shared/api/generated/browse/browse.msw';
import { browseUnitId, studentUnit } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openLockedUnit = () => {
  const unit = studentUnit();
  server.use(
    getGetStudentUnitMockHandler({
      ...unit,
      lessons: unit.lessons.map((lesson, index) => ({ ...lesson, isLocked: index > 0 })),
    }),
  );
  return renderApp(`/student/unit/${browseUnitId}`, { session: testSessions.student });
};

describe('UnitPage free tier', () => {
  it('shows a locked lesson without a link, with the badge and a subscribe link', async () => {
    openLockedUnit();

    expect(await screen.findByText('Energy')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Energy' })).toBeNull();
    expect(screen.getByText('Locked - subscribers only')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Subscribe to unlock' })).toHaveAttribute('href', '/student/subscription');
  });

  it('keeps open lessons linked', async () => {
    openLockedUnit();

    expect(await screen.findByRole('link', { name: 'Forces' })).toBeInTheDocument();
  });
});
