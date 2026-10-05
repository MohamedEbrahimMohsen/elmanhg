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
}

export function DragDropPreview({ stem, diagram }: DragDropPreviewProps) {
  const { t } = useTranslation('questionsDiagram');
  const { image } = diagram;
  const hasImage = image.url !== '' && image.width > 0 && image.height > 0;

  return (
    <div className="flex flex-col gap-4">
      <div className="text-body font-bold">
        <RichTextViewer html={stem} />
      </div>
      {hasImage ? (
        <DiagramSvg image={image} zones={diagram.zones} tone="key" label={image.alt} />
      ) : (
        <p className="text-caption text-text-muted">{t('view.diagramMissing')}</p>
      )}
      <DiagramKeyLegend diagram={diagram} />
    </div>
  );
}
