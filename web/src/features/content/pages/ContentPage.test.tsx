import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { SubjectResult } from '@/shared/api/generated/model';
import {
  getCreateSubjectMockHandler,
  getDeleteSubjectMockHandler,
  getGetSubjectsMockHandler,
  getReorderSubjectMockHandler,
  getUpdateSubjectMockHandler,
} from '@/shared/api/generated/subjects/subjects.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const physics: SubjectResult = { id: 's1', name: 'Physics', order: 1, unitCount: 2 };
const chemistry: SubjectResult = { id: 's2', name: 'Chemistry', order: 2, unitCount: 0 };

const openContent = (lng: 'en' | 'ar' = 'en') => renderApp('/admin/content', { session: testSessions.admin, lng });

const findSubjects = async () => within(await screen.findByRole('list', { name: 'Subjects' })).getAllByRole('listitem');

const findCard = async (name: string) => {
  const cards = await findSubjects();
  const card = cards.find((item) => within(item).queryByRole('heading', { name }) !== null);
  if (!card) {
    throw new Error(`No subject card named ${name}.`);
  }
  return within(card);
};

describe('ContentPage', () => {
  let subjects: SubjectResult[];
  let bodies: unknown[];

  beforeEach(() => {
    subjects = [physics, chemistry];
    bodies = [];
    server.use(getGetSubjectsMockHandler(() => subjects));
  });

  const captureBody = async ({ request }: { request: Request }) => {
    bodies.push(await request.json());
  };

  it('shows subjects in order after loading', async () => {
    openContent();

    expect(await screen.findByRole('status', { name: 'Loading subjects' })).toBeInTheDocument();
    const [first, second] = await findSubjects();
    expect(first).toHaveTextContent('Physics');
    expect(first).toHaveTextContent('2 units');
    expect(second).toHaveTextContent('Chemistry');
  });

  it('shows the empty state when there are no subjects', async () => {
    subjects = [];
    openContent();

    expect(await screen.findByText('No subjects yet. Add the first subject.')).toBeInTheDocument();
  });

  it('shows an error and recovers on retry', async () => {
    server.use(http.get('*/api/subjects', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })));
    const user = userEvent.setup();
    openContent();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the content tree');
    server.use(getGetSubjectsMockHandler(() => subjects));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await findSubjects()).toHaveLength(2);
  });

  it('adds a subject and refreshes the list', async () => {
    server.use(
      getCreateSubjectMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        subjects = [...subjects, { id: 's3', name: 'Biology', order: 3, unitCount: 0 }];
        return { id: 's3' };
      }),
    );
    const user = userEvent.setup();
    openContent();

    await findSubjects();
    await user.type(screen.getByLabelText('Subject name'), 'Biology');
    await user.click(screen.getByRole('button', { name: 'Add subject' }));

    expect(await screen.findByRole('heading', { name: 'Biology' })).toBeInTheDocument();
    expect(bodies).toEqual([{ name: 'Biology' }]);
    expect(await screen.findByText('Subject added.')).toBeInTheDocument();
    expect(screen.getByLabelText('Subject name')).toHaveValue('');
  });

  it('shows the required error without calling the API', async () => {
    server.use(
      getCreateSubjectMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        return { id: 's3' };
      }),
    );
    const user = userEvent.setup();
    openContent();

    await findSubjects();
    await user.click(screen.getByRole('button', { name: 'Add subject' }));

    expect(await screen.findByText('This field is required.')).toBeInTheDocument();
    expect(bodies).toEqual([]);
  });

  it('shows a server name error inline', async () => {
    server.use(
      http.post('*/api/subjects', () => HttpResponse.json({ code: 'SUBJECT_NAME_TOO_LONG' }, { status: 422 })),
    );
    const user = userEvent.setup();
    openContent();

    await findSubjects();
    await user.type(screen.getByLabelText('Subject name'), 'Biology');
    await user.click(screen.getByRole('button', { name: 'Add subject' }));

    expect(await screen.findByText('Subject name is too long.')).toBeInTheDocument();
    expect(screen.getByLabelText('Subject name')).toHaveAttribute('aria-invalid', 'true');
  });

  it('moves a subject down', async () => {
    server.use(getReorderSubjectMockHandler(captureBody));
    const user = userEvent.setup();
    openContent();

    await findSubjects();
    expect(screen.getByRole('button', { name: 'Move Physics up' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Move Chemistry down' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Move Physics down' }));

    expect(await screen.findByText('Subject moved.')).toBeInTheDocument();
    expect(bodies).toEqual([{ position: 2 }]);
  });

  it('renames a subject', async () => {
    server.use(getUpdateSubjectMockHandler(captureBody));
    const user = userEvent.setup();
    openContent();

    const card = await findCard('Physics');
    await user.click(card.getByRole('button', { name: 'Rename' }));
    await user.clear(card.getByLabelText('New name'));
    await user.type(card.getByLabelText('New name'), 'Mechanics');
    await user.click(card.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Subject renamed.')).toBeInTheDocument();
    expect(bodies).toEqual([{ name: 'Mechanics' }]);
  });

  it('deletes a subject after confirmation', async () => {
    const deleted: string[] = [];
    server.use(
      getDeleteSubjectMockHandler(({ params }) => {
        deleted.push(String(params.subjectId));
      }),
    );
    const user = userEvent.setup();
    openContent();

    const card = await findCard('Physics');
    await user.click(card.getByRole('button', { name: 'Delete' }));
    expect(card.getByText('Delete Physics?')).toBeInTheDocument();
    await user.click(card.getByRole('button', { name: 'Cancel' }));
    expect(deleted).toEqual([]);
    await user.click(card.getByRole('button', { name: 'Delete' }));
    await user.click(card.getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('Subject deleted.')).toBeInTheDocument();
    expect(deleted).toEqual(['s1']);
  });

  it('shows an error toast when a subject with units cannot be deleted', async () => {
    server.use(
      http.delete('*/api/subjects/:subjectId', () => HttpResponse.json({ code: 'SUBJECT_HAS_UNITS' }, { status: 400 })),
    );
    const user = userEvent.setup();
    openContent();

    const card = await findCard('Physics');
    await user.click(card.getByRole('button', { name: 'Delete' }));
    await user.click(card.getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('This subject has units. Delete its units first.')).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    openContent('ar');

    expect(await screen.findByRole('heading', { name: 'شجرة المحتوى' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openContent();

    await findSubjects();

    expect((await axe(container)).violations).toEqual([]);
  });
});
