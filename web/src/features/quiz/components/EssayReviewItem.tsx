import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import type { SessionItemResult } from '@/shared/api/generated/model';
import { essayAnswerText, type EssayItem } from '../api/essayItem';
import { EssayAnswerText } from './EssayAnswerText';
import { EssayGradeStatus } from './EssayGradeStatus';

export interface EssayReviewItemProps {
  sessionId: string;
  item: EssayItem & Pick<SessionItemResult, 'position' | 'questionId' | 'stem' | 'explanation'>;
  onGraded?: (() => void) | undefined;
}

export function EssayReviewItem({ sessionId, item, onGraded }: EssayReviewItemProps) {
  const { t } = useTranslation('quiz');
  const headingId = useId();

  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4.5 shadow-1"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h3 id={headingId} className="font-display text-h3 font-bold">
          {t('result.reviewItem', { position: Number(item.position) })}
        </h3>
        <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-bold text-text-muted">
          {t('questions:types.Essay')}
        </span>
      </div>
      <div className="text-body font-bold">
        <RichTextViewer html={item.stem} />
      </div>
      <EssayAnswerText text={essayAnswerText(item)} />
      <EssayGradeStatus sessionId={sessionId} questionId={item.questionId} onGraded={onGraded} />
      {item.explanation ? (
        <div className="flex flex-col gap-1">
          <p className="text-caption font-bold text-text">{t('feedback.explanation')}</p>
          <div className="text-ui text-text-muted">
            <RichTextViewer html={item.explanation} />
          </div>
        </div>
      ) : null}
    </article>
  );
}
