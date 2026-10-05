import { useTranslation } from 'react-i18next';
import type { DiagramModel } from '../api/dragDropValues';
import { registerDiagramLocales } from '../diagramLocales';

registerDiagramLocales();

export interface DiagramKeyLegendProps {
  diagram: DiagramModel;
}

export function DiagramKeyLegend({ diagram }: DiagramKeyLegendProps) {
  const { t } = useTranslation('questionsDiagram');
  const textOf = new Map(diagram.items.map((item) => [item.id, item.text]));
  const textsOf = (ids: readonly string[]) =>
    ids.map((id) => textOf.get(id)).filter((text): text is string => text !== undefined);
  const placed = new Set(diagram.zones.flatMap((zone) => zone.itemIds));
  const distractors = diagram.items.filter((item) => !placed.has(item.id)).map((item) => item.text);

  return (
    <section aria-label={t('view.diagramKeyTitle')} className="flex flex-col gap-2">
      <h3 className="text-h3 font-bold">{t('view.diagramKeyTitle')}</h3>
      <ol className="flex flex-col gap-1 text-ui">
        {diagram.zones.map((zone, index) => {
          const number = index + 1;
          const texts = textsOf(zone.itemIds);
          return (
            <li key={zone.id}>
              {texts.length === 0
                ? t('view.diagramKeyEmpty', { number })
                : zone.ordered
                  ? t('view.diagramKeyOrdered', { number, items: texts.join(t('view.orderSeparator')) })
                  : t('view.diagramKeyZone', { number, items: texts.join(t('view.listSeparator')) })}
            </li>
          );
        })}
      </ol>
      {distractors.length > 0 ? (
        <p className="text-ui">{t('view.diagramDistractors', { items: distractors.join(t('view.listSeparator')) })}</p>
      ) : null}
    </section>
  );
}
