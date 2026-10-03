import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { GradeReviewDetailResult, GradeReviewNoteResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { formatNumber } from '@/shared/lib/format';

export interface ReviewedCardProps {
  detail: GradeReviewDetailResult;
  review: GradeReviewNoteResult;
}

export function ReviewedCard({ detail, review }: ReviewedCardProps) {
  const { t, i18n } = useTranslation('gradeReview');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const headingId = useId();

  return (
    <section
      role="status"
      aria-labelledby={headingId}
      className="flex flex-col gap-2 rounded-lg border border-border bg-soft p-4 lg:p-5"
    >
      <h2 id={headingId} className="font-display text-h3 font-semibold">
        {t('detail.reviewed')}
      </h2>
      <p className="text-ui font-semibold text-text">
        {t(`decisions.${review.decision}`, { defaultValue: review.decision })}
      </p>
      <p className="text-ui text-text">
        {t('detail.finalScore', {
          score: formatNumber(Number(detail.finalScore), lng),
          maxScore: formatNumber(Number(detail.maxScore), lng),
        })}
      </p>
      {review.comment ? (
        <p dir="auto" className="text-ui text-text-muted">
          {review.comment}
        </p>
      ) : null}
      <p className="text-caption text-text-muted">
        {t('detail.reviewedAt', {
          date: formatDateTime(review.reviewedAt, lng),
        })}
      </p>
    </section>
  );
}
