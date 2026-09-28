import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import {
  getDeleteExamBlueprintMockHandler,
  getGetSubjectExamBlueprintsMockHandler,
} from '@/shared/api/generated/exam-blueprints/exam-blueprints.msw';
import type { SubjectExamBlueprintsResult } from '@/shared/api/generated/model';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import {
  blueprint,
  blueprintSubjectId,
  mechanicsUnitId,
  overview,
  servable,
  unitBlueprintId,
} from '@/test/blueprintFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const unitWith = (unit: Partial<SubjectExamBlueprintsResult['units'][number]>) =>
  overview({
    units: [
      {
        unitId: mechanicsUnitId,
        name: 'Mechanics',
        order: 1,
        blueprint: null,
        servable: servable({ Mcq: 1 }),
        ...unit,
      },
    ],
  });

const openUnit = async (data: SubjectExamBlueprintsResult | (() => SubjectExamBlueprintsResult)) => {
  server.use(
    getGetSubjectsMockHandler([{ id: blueprintSubjectId, name: 'Physics', order: 1, unitCount: 1 }]),
    getGetSubjectExamBlueprintsMockHandler(typeof data === 'function' ? data : () => data),
  );
  renderApp('/admin/blueprints', { session: testSessions.admin });
  return screen.findByRole('region', { name: 'Unit: Mechanics' });
};

describe('UnitBlueprintSection', () => {
  it('shows that a unit uses the default and warns when the default is short for it', async () => {
    const unit = await openUnit(unitWith({}));

    expect(within(unit).getByText('Uses the subject default.')).toBeInTheDocument();
    expect(within(unit).getByText('Current shortfall in this unit:')).toBeInTheDocument();
    expect(within(unit).getByText('Multiple choice: required 2, available 1 (1 short)')).toBeInTheDocument();
  });

  it('says no exam is available when there is no default', async () => {
    const unit = await openUnit({ ...unitWith({}), defaultBlueprint: null });

    expect(
      within(unit).getByText('The subject has no default blueprint, so this unit has no exam.'),
    ).toBeInTheDocument();
    expect(within(unit).queryByText('Current shortfall in this unit:')).toBeNull();
  });

  it('opens an editor prefilled from the default and cancels', async () => {
    const user = userEvent.setup();
    const unit = await openUnit(unitWith({}));

    await user.click(within(unit).getByRole('button', { name: 'Create a unit blueprint' }));

    const editor = screen.getByRole('region', { name: 'Unit: Mechanics' });
    expect(within(editor).getByLabelText('Required count — Multiple choice')).toHaveValue('2');
    expect(within(editor).getByLabelText('Pass mark')).toHaveValue('50');

    await user.click(within(editor).getByRole('button', { name: 'Cancel' }));

    const card = screen.getByRole('region', { name: 'Unit: Mechanics' });
    expect(within(card).getByText('Uses the subject default.')).toBeInTheDocument();
    expect(within(card).queryByLabelText('Required count — Multiple choice')).toBeNull();
  });

  it('removes a unit blueprint after confirming', async () => {
    const user = userEvent.setup();
    let deleted = false;
    const unitBlueprint = blueprint({
      id: unitBlueprintId,
      unitId: mechanicsUnitId,
      typeCounts: [{ type: 'Mcq', count: 1 }],
    });
    const unit = await openUnit(() => unitWith({ blueprint: deleted ? null : unitBlueprint }));
    server.use(
      getDeleteExamBlueprintMockHandler(({ params }) => {
        deleted = params.examBlueprintId === unitBlueprintId;
      }),
    );

    await user.click(within(unit).getByRole('button', { name: 'Use the subject default' }));
    expect(
      within(unit).getByText("Remove this unit's blueprint? The unit will use the subject default."),
    ).toBeInTheDocument();
    await user.click(within(unit).getByRole('button', { name: 'Confirm' }));

    expect(await screen.findByText('Unit blueprint removed')).toBeInTheDocument();
    expect(await screen.findByText('Uses the subject default.')).toBeInTheDocument();
  });
});
