import { useState } from 'react';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from '@/test/axe';
import { renderWithProviders } from '@/test/renderWithProviders';
import type { DiagramPlacements } from '../api/diagramPlacement';
import { emptyAnswer, type ChoiceReview, type StudentQuestion } from '../api/studentQuestion';
import { DragDropAnswerInput } from './DragDropAnswerInput';

const question: StudentQuestion = {
  type: 'DragDrop',
  stem: '<p>Label the plant cell.</p>',
  options: [],
  blankIds: [],
  answerKind: null,
  diagram: {
    image: { url: '/api/media/question-diagrams/l/abc.png', width: 800, height: 600, alt: 'Plant cell' },
    zones: [
      { id: 'z1', x: 10, y: 10, width: 20, height: 15, capacity: 2 },
      { id: 'z2', x: 50, y: 40, width: 30, height: 20.5, capacity: 2 },
    ],
    items: [
      { id: 'i1', text: 'Nucleus' },
      { id: 'i2', text: 'Vacuole' },
      { id: 'i3', text: 'Wall' },
      { id: 'i4', text: 'Membrane' },
      { id: 'i5', text: 'Engine' },
    ],
  },
};

const review: ChoiceReview = {
  correctKeys: [],
  diagramKey: [
    { zoneId: 'z1', itemIds: ['i1', 'i2'], ordered: false },
    { zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true },
  ],
};

interface HarnessProps {
  target?: StudentQuestion;
  initial?: DiagramPlacements;
  disabled?: boolean;
}

function Harness({ target = question, initial = {}, disabled }: HarnessProps) {
  const [answer, setAnswer] = useState({ ...emptyAnswer(), placements: initial });

  return (
    <DragDropAnswerInput
      question={target}
      answer={answer}
      onAnswerChange={setAnswer}
      disabled={disabled}
      review={disabled ? review : undefined}
    />
  );
}

const zoneRow = (number: number) => within(screen.getByRole('listitem', { name: `Zone ${String(number)}` }));
const bank = () => within(screen.getByRole('region', { name: 'Items to place' }));
const chip = (name: string) => screen.getByRole('button', { name });

function mockCanvasBounds() {
  vi.spyOn(screen.getByRole('group', { name: 'Diagram' }), 'getBoundingClientRect').mockReturnValue(
    DOMRect.fromRect({ x: 0, y: 0, width: 800, height: 600 }),
  );
}

async function dragWallTo(user: UserEvent, pointer: 'MouseLeft' | 'TouchA', clientX: number, clientY: number) {
  await user.pointer([
    { keys: `[${pointer}>]`, target: chip('Wall'), coords: { clientX: 400, clientY: 700 } },
    { pointerName: pointer === 'TouchA' ? 'TouchA' : 'mouse', coords: { clientX: 300, clientY: 400 } },
    { pointerName: pointer === 'TouchA' ? 'TouchA' : 'mouse', coords: { clientX, clientY } },
    { keys: `[/${pointer}]` },
  ]);
}

async function tapPlace(user: UserEvent, item: string, zone: number) {
  await user.click(chip(item));
  await user.click(zoneRow(zone).getByRole('button', { name: `Place here (${item} in zone ${String(zone)})` }));
}

describe('DragDropAnswerInput', () => {
  it('places the selected item from the zone list with the keyboard', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    chip('Nucleus').focus();
    await user.keyboard('{Enter}');
    expect(chip('Nucleus')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('status')).toHaveTextContent('Nucleus selected. Choose a zone.');
    zoneRow(1).getByRole('button', { name: 'Place here (Nucleus in zone 1)' }).focus();
    await user.keyboard('{Enter}');

    expect(zoneRow(1).getByRole('button', { name: 'Nucleus' })).toBeInTheDocument();
    await waitFor(() => {
      expect(chip('Nucleus')).toHaveFocus();
    });
    expect(screen.getByRole('status')).toHaveTextContent('Nucleus placed in zone 1, position 1.');
  });

  it('places the selected item by tapping a zone on the diagram', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.click(chip('Vacuole'));
    await user.click(screen.getByRole('button', { name: 'Zone 2: 0 of 2 items' }));

    expect(zoneRow(2).getByRole('button', { name: 'Vacuole' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Zone 2: 1 of 2 items' })).toBeInTheDocument();
  });

  it('drags an item onto a zone of the diagram with a pointer', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    const canvas = screen.getByRole('group', { name: 'Diagram' });
    vi.spyOn(canvas, 'getBoundingClientRect').mockReturnValue(
      DOMRect.fromRect({ x: 0, y: 0, width: 800, height: 600 }),
    );

    await user.pointer([
      { keys: '[MouseLeft>]', target: chip('Wall'), coords: { clientX: 400, clientY: 700 } },
      { coords: { clientX: 300, clientY: 400 } },
      { coords: { clientX: 200, clientY: 100 } },
      { keys: '[/MouseLeft]' },
    ]);

    expect(zoneRow(1).getByRole('button', { name: 'Wall' })).toHaveAttribute('aria-pressed', 'false');
    expect(bank().queryByRole('button', { name: 'Wall' })).not.toBeInTheDocument();
  });

  it('selects the next tapped chip after a mouse drag', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    mockCanvasBounds();
    await dragWallTo(user, 'MouseLeft', 200, 100);

    await user.click(chip('Nucleus'));

    expect(chip('Nucleus')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('status')).toHaveTextContent('Nucleus selected. Choose a zone.');
  });

  it('selects the next tapped chip after a touch drag', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    mockCanvasBounds();
    await dragWallTo(user, 'TouchA', 200, 100);

    await user.pointer([{ keys: '[TouchA]', target: chip('Nucleus') }]);

    expect(zoneRow(1).getByRole('button', { name: 'Wall' })).toBeInTheDocument();
    expect(chip('Nucleus')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('status')).toHaveTextContent('Nucleus selected. Choose a zone.');
  });

  it('selects the remounted chip with Enter after a drop', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    mockCanvasBounds();
    await dragWallTo(user, 'MouseLeft', 200, 100);

    zoneRow(1).getByRole('button', { name: 'Wall' }).focus();
    await user.keyboard('{Enter}');

    expect(zoneRow(1).getByRole('button', { name: 'Wall' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('status')).toHaveTextContent('Wall selected. Choose a zone.');
  });

  it('does not select the chip when a mouse drag ends outside every target', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    mockCanvasBounds();

    await dragWallTo(user, 'MouseLeft', 900, 50);

    expect(bank().getByRole('button', { name: 'Wall' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('status')).not.toHaveTextContent('Wall selected');
  });

  it('announces a full zone and keeps the item in the bank', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness initial={{ z1: ['i1', 'i2'] }} />);

    await user.click(chip('Wall'));
    await user.click(screen.getByRole('button', { name: 'Zone 1: 2 of 2 items' }));

    expect(screen.getByRole('status')).toHaveTextContent('Zone 1 is full.');
    expect(zoneRow(1).getByText('Full')).toBeInTheDocument();
    expect(bank().getByRole('button', { name: 'Wall' })).toBeInTheDocument();
  });

  it('asks for an item first when a zone is chosen with nothing selected', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Zone 1: 0 of 2 items' }));

    expect(screen.getByRole('status')).toHaveTextContent('Choose an item first.');
  });

  it('returns a placed item to the bank', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    await tapPlace(user, 'Nucleus', 1);

    await user.click(screen.getByRole('button', { name: 'Return Nucleus to the bank' }));

    expect(bank().getByRole('button', { name: 'Nucleus' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Nucleus returned to the bank.');
  });

  it('reorders items within a zone', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    await tapPlace(user, 'Nucleus', 1);
    await tapPlace(user, 'Vacuole', 1);

    await user.click(screen.getByRole('button', { name: 'Move Vacuole earlier' }));

    const names = zoneRow(1)
      .getAllByRole('button', { pressed: false })
      .map((button) => button.textContent);
    expect(names).toEqual(['Vacuole', 'Nucleus']);
    expect(screen.getByRole('status')).toHaveTextContent('Vacuole moved to position 1 in zone 1.');
  });

  it('clears the selection with Escape', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    await user.click(chip('Engine'));

    await user.keyboard('{Escape}');

    expect(chip('Engine')).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('status')).toHaveTextContent('Selection cleared.');
  });

  it('shows a mark for each item when answered with a key', () => {
    renderWithProviders(<Harness initial={{ z1: ['i1'], z2: ['i3', 'i5'] }} disabled />);

    expect(zoneRow(1).getByRole('listitem')).toHaveTextContent('Nucleus in the right place');
    const zone2 = zoneRow(2).getAllByRole('listitem');
    expect(zone2[0]).toHaveTextContent('Wall in the wrong place');
    expect(zone2[1]).toHaveTextContent('Engine in the wrong place');
    const bankItems = bank().getAllByRole('listitem');
    expect(bankItems.map((item) => item.textContent)).toEqual(['Vacuole not placed', 'Membrane not placed']);
    for (const name of ['Nucleus', 'Vacuole', 'Wall', 'Membrane', 'Engine']) {
      expect(screen.queryByRole('button', { name: new RegExp(name) })).not.toBeInTheDocument();
    }
  });

  it('keeps the diagram left to right in Arabic', async () => {
    renderWithProviders(<Harness />, { lng: 'ar' });

    const canvas = await screen.findByRole('group', { name: 'الرسم' });

    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(canvas).toHaveAttribute('dir', 'ltr');
  });

  it('shows a notice when the question has no diagram', () => {
    renderWithProviders(<Harness target={{ ...question, diagram: null }} />);

    expect(screen.getByText('The diagram image has not been uploaded yet.')).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const user = userEvent.setup();
    const { container } = renderWithProviders(<Harness initial={{ z1: ['i1', 'i2'] }} />);
    await user.click(chip('Wall'));

    expect((await axe(container)).violations).toEqual([]);
  });
});
