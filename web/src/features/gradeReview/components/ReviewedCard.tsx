import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { GradeReviewDetailResult, GradeReviewNoteResult } from '@/shared/api/generated/model';
import { formatDate, formatNumber } from '@/shared/lib/format';

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
          score: formatNumber(Number(detail.finalScore), lng, 'latin'),
          maxScore: formatNumber(Number(detail.maxScore), lng, 'latin'),
        })}
      </p>
      {review.comment ? (
        <p dir="auto" className="text-ui text-text-muted">
          {review.comment}
        </p>
      ) : null}
      <p className="text-caption text-text-muted">
        {t('detail.reviewedAt', {
          date: formatDate(new Date(review.reviewedAt), lng, 'latin', { dateStyle: 'medium', timeStyle: 'short' }),
        })}
      </p>
    </section>
  );
}
