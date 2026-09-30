import { ClipboardCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export interface GradeReviewEmptyStateProps {
  messageKey: 'queue.empty.noSubjects' | 'queue.empty.noItems';
}

export function GradeReviewEmptyState({ messageKey }: GradeReviewEmptyStateProps) {
  const { t } = useTranslation('gradeReview');

  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
      <ClipboardCheck aria-hidden className="size-8 text-text-muted" />
      <p className="text-ui text-text">{t(messageKey)}</p>
    </div>
  );
}
