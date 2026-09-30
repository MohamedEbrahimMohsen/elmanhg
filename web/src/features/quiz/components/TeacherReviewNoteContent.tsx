import { useTranslation } from 'react-i18next';
import type { GradeReviewNoteResult } from '@/shared/api/generated/model';
import { registerTeacherReviewLocales, teacherReviewNamespace } from '../teacherReviewLocales';

registerTeacherReviewLocales();

export interface TeacherReviewNoteProps {
  review: GradeReviewNoteResult;
}

export function TeacherReviewNoteContent({ review }: TeacherReviewNoteProps) {
  const { t } = useTranslation(teacherReviewNamespace);

  return (
    <div role="note" className="flex flex-col gap-1 rounded-md border border-border bg-soft px-3.5 py-3">
      <p className="text-ui font-semibold">{t(review.decision === 'Overridden' ? 'overridden' : 'accepted')}</p>
      {review.comment ? (
        <p dir="auto" className="text-ui text-text-muted">
          {t('comment', { comment: review.comment })}
        </p>
      ) : null}
    </div>
  );
}
