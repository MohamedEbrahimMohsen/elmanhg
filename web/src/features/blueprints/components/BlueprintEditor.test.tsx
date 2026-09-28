import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getGetSubjectExamBlueprintsMockHandler,
  getSaveSubjectExamBlueprintMockHandler,
} from '@/shared/api/generated/exam-blueprints/exam-blueprints.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { blueprint, blueprintSubjectId, overview, servable } from '@/test/blueprintFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openEditor = async () => {
  server.use(
    getGetSubjectsMockHandler([{ id: blueprintSubjectId, name: 'Physics', order: 1, unitCount: 1 }]),
    getGetSubjectExamBlueprintsMockHandler(overview()),
  );
  renderApp('/admin/blueprints', { session: testSessions.admin });
  return screen.findByRole('region', { name: 'Subject default blueprint' });
};

const setMcq = async (user: ReturnType<typeof userEvent.setup>, editor: HTMLElement, value: string) => {
  const input = within(editor).getByLabelText('Required count — Multiple choice');
  await user.clear(input);
  await user.type(input, value);
};

describe('BlueprintEditor', () => {
  it('highlights a short type and lists the live shortfall', async () => {
    const user = userEvent.setup();
    const editor = await openEditor();

    await setMcq(user, editor, '5');

    expect(within(editor).getByText('Current shortfall:')).toBeInTheDocument();
    expect(within(editor).getByText('Multiple choice: required 5, available 3 (2 short)')).toBeInTheDocument();
    expect(within(editor).getByRole('row', { name: /Multiple choice/ })).toHaveClass('bg-warning-soft');
  });

  it('refuses to save while a type is short', async () => {
    const user = userEvent.setup();
    const editor = await openEditor();

    await setMcq(user, editor, '5');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent(
      'Not saved: some types do not have enough servable questions.',
    );
    expect(screen.queryByText('Blueprint saved')).toBeNull();
  });

  it('saves the subject default', async () => {
    const user = userEvent.setup();
    let body: unknown;
    const editor = await openEditor();
    server.use(
      getSaveSubjectExamBlueprintMockHandler(async ({ request }) => {
        body = await request.json();
        return blueprint({ typeCounts: [{ type: 'Mcq', count: 3 }], questionCount: 3 });
      }),
    );

    await setMcq(user, editor, '3');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Blueprint saved')).toBeInTheDocument();
    expect(body).toEqual({
      typeCounts: [{ type: 'Mcq', count: 3 }],
      difficultyMix: null,
      timeLimitMinutes: 45,
      passMark: 50,
    });
  });

  it('shows the server shortfall and refreshes the available counts', async () => {
    const user = userEvent.setup();
    const editor = await openEditor();
    server.use(
      http.put('*/api/exam-blueprints/subjects/:subjectId', () =>
        HttpResponse.json({ code: 'EXAM_BLUEPRINT_SHORTFALL' }, { status: 400 }),
      ),
      getGetSubjectExamBlueprintsMockHandler(overview({ servable: servable({ Mcq: 7, Fill: 1 }) })),
    );

    await setMcq(user, editor, '3');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent(
      'Not saved: some types do not have enough servable questions.',
    );
    const row = within(editor).getByRole('row', { name: /Multiple choice/ });
    expect(await within(row).findByText('7')).toBeInTheDocument();
  });

  it('shows a server pass-mark error on the field', async () => {
    const user = userEvent.setup();
    const editor = await openEditor();
    server.use(
      http.put('*/api/exam-blueprints/subjects/:subjectId', () =>
        HttpResponse.json({ code: 'EXAM_BLUEPRINT_PASS_MARK_INVALID' }, { status: 422 }),
      ),
    );

    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(await within(editor).findByText('The pass mark must be between 1 and 100.')).toBeInTheDocument();
    expect(within(editor).getByLabelText('Pass mark')).toHaveAccessibleDescription(
      'The pass mark must be between 1 and 100.',
    );
  });
});
