import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { placedZoneOf, unplacedItems } from '../api/diagramPlacement';
import { markPlacements } from '../api/diagramReview';
import type { ChoiceReview, QuestionAnswer, StudentQuestion } from '../api/studentQuestion';
import { registerDiagramStudentLocales } from '../diagramStudentLocales';
import { useDiagramAnswer } from '../hooks/useDiagramAnswer';
import { useDiagramDrag } from '../hooks/useDiagramDrag';
import type { StudentDiagram } from '../schemas/studentDiagramSchema';
import { DiagramAnswerCanvas } from './DiagramAnswerCanvas';
import { DiagramDragGhost } from './DiagramDragGhost';
import { DiagramItemBank } from './DiagramItemBank';
import { DiagramZoneList } from './DiagramZoneList';

registerDiagramStudentLocales();

export interface DragDropAnswerInputProps {
  question: StudentQuestion;
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean | undefined;
  review?: ChoiceReview | undefined;
}

export function DragDropAnswerInput({ question, ...props }: DragDropAnswerInputProps) {
  const { t } = useTranslation('diagramStudent');

  if (!question.diagram) {
    return <p className="text-caption text-text-muted">{t('missing')}</p>;
  }
  return <DiagramAnswer diagram={question.diagram} {...props} />;
}

interface DiagramAnswerProps extends Omit<DragDropAnswerInputProps, 'question'> {
  diagram: StudentDiagram;
}

function DiagramAnswer({ diagram, answer, onAnswerChange, disabled, review }: DiagramAnswerProps) {
  const { t } = useTranslation('diagramStudent');
  const interactive = !disabled;
  const placements = answer.placements;
  const marks = disabled && review?.diagramKey ? markPlacements(review.diagramKey, placements) : undefined;
  const controls = useDiagramAnswer(diagram, placements, (next) => {
    onAnswerChange({ ...answer, placements: next });
  });
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const { drag, chipHandlers } = useDiagramDrag({
    canvasRef,
    zones: diagram.zones,
    onDropZone: controls.place,
    onDropBank: (itemId) => {
      if (placedZoneOf(placements, itemId) !== null) {
        controls.returnToBank(itemId);
      }
    },
  });
  const chips = useRef(new Map<string, HTMLButtonElement>());
  const { focusItemId } = controls;

  useEffect(() => {
    if (focusItemId !== null) {
      chips.current.get(focusItemId)?.focus();
    }
  }, [focusItemId, placements]);

  const chipProps = (itemId: string) => ({
    selected: controls.selectedItemId === itemId,
    handlers: chipHandlers(itemId),
    onSelect: controls.select,
    chipRef: (element: HTMLButtonElement | null) => {
      if (element) {
        chips.current.set(itemId, element);
      } else {
        chips.current.delete(itemId);
      }
    },
  });
  const dragText = drag ? diagram.items.find((item) => item.id === drag.itemId)?.text : undefined;

  return (
    <div className="flex flex-col gap-4">
      {interactive ? <p className="text-caption text-text-muted">{t('hint')}</p> : null}
      <DiagramAnswerCanvas
        diagram={diagram}
        placements={placements}
        overZoneId={drag?.overZoneId ?? null}
        selectedItemId={controls.selectedItemId}
        interactive={interactive}
        onZone={controls.placeSelected}
        canvasRef={canvasRef}
      />
      <DiagramZoneList
        diagram={diagram}
        placements={placements}
        selectedItemId={controls.selectedItemId}
        overZoneId={drag?.overZoneId ?? null}
        interactive={interactive}
        marks={marks}
        chipProps={chipProps}
        onPlace={controls.placeSelected}
        onReturn={controls.returnToBank}
        onMove={controls.move}
      />
      <DiagramItemBank
        items={unplacedItems(diagram, placements)}
        interactive={interactive}
        marks={marks}
        chipProps={chipProps}
      />
      <p role="status" className="sr-only">
        {controls.announcement}
      </p>
      {drag && dragText !== undefined ? <DiagramDragGhost text={dragText} x={drag.x} y={drag.y} /> : null}
    </div>
  );
}
