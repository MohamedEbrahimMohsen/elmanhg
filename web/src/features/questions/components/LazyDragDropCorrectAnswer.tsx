import { lazy, Suspense } from 'react';
import type { DragDropCorrectAnswerProps } from './DragDropCorrectAnswer';

const DragDropCorrectAnswer = lazy(async () => ({
  default: (await import('./DragDropCorrectAnswer')).DragDropCorrectAnswer,
}));

export function LazyDragDropCorrectAnswer(props: DragDropCorrectAnswerProps) {
  return (
    <Suspense fallback={<div aria-busy="true" className="min-h-11" />}>
      <DragDropCorrectAnswer {...props} />
    </Suspense>
  );
}
