import { useTranslation } from 'react-i18next';
import type { GradeReviewNoteResult } from '@/shared/api/generated/model';

export interface TeacherReviewNoteProps {
  review: GradeReviewNoteResult;
}

export function TeacherReviewNote({ review }: TeacherReviewNoteProps) {
  const { t } = useTranslation('quiz');

  return (
    <div role="note" className="flex flex-col gap-1 rounded-md border border-border bg-soft px-3.5 py-3">
      <p className="text-ui font-semibold">
        {t(review.decision === 'Overridden' ? 'teacherReview.overridden' : 'teacherReview.accepted')}
      </p>
      {review.comment ? (
        <p dir="auto" className="text-ui text-text-muted">
          {t('teacherReview.comment', { comment: review.comment })}
        </p>
      ) : null}
    </div>
  );
}
