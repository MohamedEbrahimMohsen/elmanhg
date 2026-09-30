import type { DeepPartialSkipArrayKey } from 'react-hook-form';
import type { StudentDiagram } from '../schemas/studentDiagramSchema';
import type { QuestionValues } from '../schemas/questionEditorSchema';

const toNumber = (value: string | number | undefined) => (Number.isFinite(Number(value)) ? Number(value) : 0);

export function studentDiagramFromValues(values: DeepPartialSkipArrayKey<QuestionValues>): StudentDiagram | null {
  const image = values.diagramImage;
  const width = toNumber(image?.width);
  const height = toNumber(image?.height);
  if (!image?.url || width <= 0 || height <= 0) {
    return null;
  }
  return {
    image: { url: image.url, width, height, alt: image.alt ?? '' },
    zones: (values.diagramZones ?? []).map((zone) => ({
      id: zone.id ?? '',
      x: toNumber(zone.x),
      y: toNumber(zone.y),
      width: toNumber(zone.width),
      height: toNumber(zone.height),
      capacity: Math.max(1, toNumber(zone.capacity)),
    })),
    items: (values.diagramItems ?? [])
      .map((item) => ({ id: item.id ?? '', text: item.text ?? '' }))
      .filter((item) => item.text.trim() !== ''),
  };
}
