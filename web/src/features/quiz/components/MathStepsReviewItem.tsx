import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import { MathStepsReadOnly } from '@/features/questions';
import type { SessionItemResult } from '@/shared/api/generated/model';
import { mathStepsAnswerOf, type MathStepsItem } from '../api/mathStepsItem';
import { MathStepGradeStatus } from './MathStepGradeStatus';

export interface MathStepsReviewItemProps {
  sessionId: string;
  item: MathStepsItem & Pick<SessionItemResult, 'position' | 'questionId' | 'stem'>;
  onGraded?: (() => void) | undefined;
}

export function MathStepsReviewItem({ sessionId, item, onGraded }: MathStepsReviewItemProps) {
  const { t } = useTranslation('quiz');
  const headingId = useId();

  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4.5 shadow-1"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h3 id={headingId} className="font-display text-h3 font-semibold">
          {t('result.reviewItem', { position: Number(item.position) })}
        </h3>
        <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">
          {t('questions:types.MathSteps')}
        </span>
      </div>
      <div className="text-body font-semibold">
        <RichTextViewer html={item.stem} />
      </div>
      <MathStepsReadOnly solution={mathStepsAnswerOf(item)} />
      <MathStepGradeStatus sessionId={sessionId} questionId={item.questionId} onGraded={onGraded} />
    </article>
  );
}
