import { describe, expect, it } from 'vitest';
import { diagramKeySchema, studentDiagramBodySchema } from './studentDiagramSchema';

const body = {
  image: {
    key: 'question-diagrams/l/abc.png',
    url: '/api/media/question-diagrams/l/abc.png',
    width: 800,
    height: 600,
    alt: 'Plant cell',
  },
  zones: [{ id: 'z1', x: 10, y: 10, width: 20, height: 15, capacity: 2 }],
  items: [{ id: 'i1', text: 'Nucleus' }],
};

describe('studentDiagramSchema', () => {
  it('reads a served diagram body', () => {
    const parsed = studentDiagramBodySchema.parse(body);

    expect(parsed.image.url).toBe('/api/media/question-diagrams/l/abc.png');
    expect(parsed.zones[0]?.capacity).toBe(2);
  });

  it('rejects a body without an image url', () => {
    const image = { ...body.image, url: undefined };

    expect(studentDiagramBodySchema.safeParse({ ...body, image }).success).toBe(false);
  });

  it('reads a diagram key', () => {
    const parsed = diagramKeySchema.parse({ zones: [{ zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true }] });

    expect(parsed.zones).toEqual([{ zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true }]);
  });
});
