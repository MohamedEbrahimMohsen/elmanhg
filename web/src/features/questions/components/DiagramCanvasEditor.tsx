import { useTranslation } from 'react-i18next';
import type { DiagramRect } from '../api/dragDropValues';
import { toDiagramModel } from '../api/dragDropValues';
import { useZoneDrawing } from '../hooks/useZoneDrawing';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { DiagramSvg } from './DiagramSvg';

export interface DiagramCanvasEditorProps {
  image: QuestionValues['diagramImage'];
  zones: QuestionValues['diagramZones'];
  canDraw: boolean;
  onDraw: (rect: DiagramRect) => void;
}

export function DiagramCanvasEditor({ image, zones, canDraw, onDraw }: DiagramCanvasEditorProps) {
  const { t } = useTranslation('questionsDiagram');
  const drawing = useZoneDrawing(onDraw, canDraw);

  if (image.url === '') {
    return <p className="text-caption text-text-muted">{t('editor.dragDrop.noImage')}</p>;
  }

  return (
    <div className="flex flex-col gap-2">
      <DiagramSvg
        image={image}
        zones={toDiagramModel({ diagramZones: zones }).zones}
        tone="edit"
        label={t('editor.dragDrop.canvasLabel')}
        draft={drawing.draft}
        interactive={canDraw}
        {...drawing.handlers}
      />
      <p className="text-caption text-text-muted">{t('editor.dragDrop.canvasHint')}</p>
    </div>
  );
}
