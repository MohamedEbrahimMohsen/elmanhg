import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import type { DiagramKey, StudentDiagram } from '../schemas/studentDiagramSchema';
import { DragDropCorrectAnswer } from './DragDropCorrectAnswer';

const diagram: StudentDiagram = {
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
};

const diagramKey: DiagramKey = [
  { zoneId: 'z1', itemIds: ['i1', 'i2'], ordered: false },
  { zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true },
];

describe('DragDropCorrectAnswer', () => {
  it("lists each zone's correct items, ordered zones in order", () => {
    renderWithProviders(<DragDropCorrectAnswer diagram={diagram} diagramKey={diagramKey} />);

    expect(screen.getByText('Zone 1: Nucleus, Vacuole')).toBeInTheDocument();
    expect(screen.getByText('Zone 2, in order: Membrane → Wall')).toBeInTheDocument();
  });

  it('lists empty zones and the items that stay in the bank', () => {
    const withEmptyZone = {
      ...diagram,
      zones: [...diagram.zones, { id: 'z3', x: 70, y: 70, width: 10, height: 10, capacity: 1 }],
    };

    renderWithProviders(
      <DragDropCorrectAnswer
        diagram={withEmptyZone}
        diagramKey={[...diagramKey, { zoneId: 'z3', itemIds: [], ordered: false }]}
      />,
    );

    expect(screen.getByText('Zone 3: stays empty')).toBeInTheDocument();
    expect(screen.getByText('Stay in the bank: Engine')).toBeInTheDocument();
  });

  it('labels the key diagram with the image description', () => {
    renderWithProviders(<DragDropCorrectAnswer diagram={diagram} diagramKey={diagramKey} />);

    expect(screen.getByRole('img', { name: 'Correct placements: Plant cell' })).toBeInTheDocument();
  });
});
