import { useTranslation } from 'react-i18next';
import { registerDiagramStudentLocales } from '../diagramStudentLocales';
import type { DiagramKey, StudentDiagram } from '../schemas/studentDiagramSchema';
import { DiagramSvg } from './DiagramSvg';

registerDiagramStudentLocales();

export interface DragDropCorrectAnswerProps {
  diagram: StudentDiagram;
  diagramKey: DiagramKey;
}

export function DragDropCorrectAnswer({ diagram, diagramKey }: DragDropCorrectAnswerProps) {
  const { t } = useTranslation('diagramStudent');
  const textOf = new Map(diagram.items.map((item) => [item.id, item.text]));
  const textsOf = (ids: readonly string[]) => ids.flatMap((id) => textOf.get(id) ?? []);
  const keyed = new Set(diagramKey.flatMap((zone) => zone.itemIds));
  const distractors = diagram.items.filter((item) => !keyed.has(item.id)).map((item) => item.text);

  return (
    <div className="flex flex-col gap-2">
      <DiagramSvg
        image={diagram.image}
        zones={diagram.zones}
        tone="key"
        label={t('key.canvasLabel', { alt: diagram.image.alt })}
      />
      <ol className="flex flex-col gap-1 text-ui text-text">
        {diagram.zones.map((zone, index) => {
          const number = index + 1;
          const key = diagramKey.find((entry) => entry.zoneId === zone.id);
          const texts = textsOf(key?.itemIds ?? []);
          return (
            <li key={zone.id}>
              {texts.length === 0
                ? t('key.zoneEmpty', { number })
                : key?.ordered
                  ? t('key.zoneOrdered', { number, items: texts.join(t('key.orderSeparator')) })
                  : t('key.zone', { number, items: texts.join(t('key.listSeparator')) })}
            </li>
          );
        })}
      </ol>
      {distractors.length > 0 ? (
        <p className="text-ui text-text">{t('key.distractors', { items: distractors.join(t('key.listSeparator')) })}</p>
      ) : null}
    </div>
  );
}
