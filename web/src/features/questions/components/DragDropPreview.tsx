import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import type { DiagramModel } from '../api/dragDropValues';
import { DiagramKeyLegend } from './DiagramKeyLegend';
import { DiagramSvg } from './DiagramSvg';
import { registerDiagramLocales } from '../diagramLocales';

registerDiagramLocales();

export interface DragDropPreviewProps {
  stem: string;
  diagram: DiagramModel;
  showKey: boolean;
}

export function DragDropPreview({ stem, diagram, showKey }: DragDropPreviewProps) {
  const { t } = useTranslation('questionsDiagram');
  const { image } = diagram;
  const hasImage = image.url !== '' && image.width > 0 && image.height > 0;

  return (
    <div className="flex flex-col gap-4">
      <div className="text-body font-semibold">
        <RichTextViewer html={stem} />
      </div>
      {hasImage ? (
        <DiagramSvg image={image} zones={diagram.zones} tone={showKey ? 'key' : 'student'} label={image.alt} />
      ) : (
        <p className="text-caption text-text-muted">{t('view.diagramMissing')}</p>
      )}
      {showKey ? (
        <DiagramKeyLegend diagram={diagram} />
      ) : (
        <section aria-label={t('view.diagramBank')} className="flex flex-col gap-2">
          <h3 className="text-h3 font-semibold">{t('view.diagramBank')}</h3>
          <ul className="flex flex-wrap gap-2">
            {diagram.items.map((item) => (
              <li key={item.id} className="rounded-full border border-border-strong bg-surface px-3 py-1.5 text-ui">
                {item.text}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
