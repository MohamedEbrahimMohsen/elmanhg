import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { SubjectDetailResult } from '@/shared/api/generated/model';
import { getGetSubjectMockHandler, getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import {
  getCreateUnitMockHandler,
  getDeleteUnitMockHandler,
  getReorderUnitMockHandler,
  getUpdateUnitMockHandler,
} from '@/shared/api/generated/units/units.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const detail = (units: SubjectDetailResult['units']): SubjectDetailResult => ({
  id: 's1',
  name: 'Physics',
  order: 1,
  units,
});

const mechanics = { id: 'u1', subjectId: 's1', name: 'Mechanics', order: 1, lessonCount: 0 };
const waves = { id: 'u2', subjectId: 's1', name: 'Waves', order: 2, lessonCount: 0 };

const expandPhysics = async (user: ReturnType<typeof userEvent.setup>) => {
  const toggle = await screen.findByRole('button', { name: 'Show units' });
  await user.click(toggle);
  return toggle;
};

const openContent = () => renderApp('/admin/content', { session: testSessions.admin });

const findUnits = async () => within(await screen.findByRole('list', { name: 'Units of Physics' }));

describe('UnitPanel', () => {
  let bodies: unknown[];

  beforeEach(() => {
    bodies = [];
    server.use(
      getGetSubjectsMockHandler([{ id: 's1', name: 'Physics', order: 1, unitCount: 2 }]),
      getGetSubjectMockHandler(detail([mechanics, waves])),
    );
  });

  it('shows the units of an expanded subject', async () => {
    const user = userEvent.setup();
    openContent();

    const toggle = await expandPhysics(user);

    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    const [first, second] = (await findUnits()).getAllByRole('listitem');
    expect(first).toHaveTextContent('Mechanics');
    expect(second).toHaveTextContent('Waves');
  });

  it('shows the empty state for a subject without units', async () => {
    server.use(getGetSubjectMockHandler(detail([])));
    const user = userEvent.setup();
    openContent();

    await expandPhysics(user);

    expect(await screen.findByText('No units in this subject yet.')).toBeInTheDocument();
  });

  it('shows an error and recovers on retry', async () => {
    server.use(
      http.get('*/api/subjects/:subjectId', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    openContent();

    await expandPhysics(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load units');
    server.use(getGetSubjectMockHandler(detail([mechanics, waves])));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect((await findUnits()).getAllByRole('listitem')).toHaveLength(2);
  });

  it('adds a unit', async () => {
    const paths: string[] = [];
    server.use(
      getCreateUnitMockHandler(async ({ request }) => {
        paths.push(new URL(request.url).pathname);
        bodies.push(await request.json());
        return { id: 'u3' };
      }),
    );
    const user = userEvent.setup();
    openContent();

    await expandPhysics(user);
    await user.type(await screen.findByLabelText('Unit name'), 'Optics');
    await user.click(screen.getByRole('button', { name: 'Add unit' }));

    expect(await screen.findByText('Unit added.')).toBeInTheDocument();
    expect(paths).toEqual(['/api/subjects/s1/units']);
    expect(bodies).toEqual([{ name: 'Optics' }]);
  });

  it('moves a unit up', async () => {
    const paths: string[] = [];
    server.use(
      getReorderUnitMockHandler(async ({ request }) => {
        paths.push(new URL(request.url).pathname);
        bodies.push(await request.json());
      }),
    );
    const user = userEvent.setup();
    openContent();

    await expandPhysics(user);
    await user.click((await findUnits()).getByRole('button', { name: 'Move Waves up' }));

    expect(await screen.findByText('Unit moved.')).toBeInTheDocument();
    expect(paths).toEqual(['/api/subjects/s1/units/u2/position']);
    expect(bodies).toEqual([{ position: 1 }]);
  });

  it('renames a unit', async () => {
    const paths: string[] = [];
    server.use(
      getUpdateUnitMockHandler(async ({ request }) => {
        paths.push(new URL(request.url).pathname);
        bodies.push(await request.json());
      }),
    );
    const user = userEvent.setup();
    openContent();

    await expandPhysics(user);
    const units = await findUnits();
    const [first] = units.getAllByRole('listitem');
    if (!first) {
      throw new Error('The unit list is empty.');
    }
    await user.click(within(first).getByRole('button', { name: 'Rename' }));
    await user.clear(within(first).getByLabelText('New name'));
    await user.type(within(first).getByLabelText('New name'), 'Kinematics');
    await user.click(within(first).getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Unit renamed.')).toBeInTheDocument();
    expect(paths).toEqual(['/api/subjects/s1/units/u1']);
    expect(bodies).toEqual([{ name: 'Kinematics' }]);
  });

  it('deletes a unit after confirmation', async () => {
    const deleted: string[] = [];
    server.use(
      getDeleteUnitMockHandler(({ params }) => {
        deleted.push(String(params.unitId));
      }),
    );
    const user = userEvent.setup();
    openContent();

    await expandPhysics(user);
    const [, second] = (await findUnits()).getAllByRole('listitem');
    if (!second) {
      throw new Error('The unit list has one item.');
    }
    await user.click(within(second).getByRole('button', { name: 'Delete' }));
    await user.click(within(second).getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('Unit deleted.')).toBeInTheDocument();
    expect(deleted).toEqual(['u2']);
  });

  it('has no axe violations when expanded', async () => {
    const user = userEvent.setup();
    const { container } = openContent();

    await expandPhysics(user);
    await findUnits();

    expect((await axe(container)).violations).toEqual([]);
  });
});
