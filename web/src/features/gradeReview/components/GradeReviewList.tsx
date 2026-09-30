import { useTranslation } from 'react-i18next';
import type { GradeReviewItemResult } from '@/shared/api/generated/model';
import { GradeReviewListItem } from './GradeReviewListItem';

export interface GradeReviewListProps {
  items: GradeReviewItemResult[];
  subjectId: string;
  now: Date;
}

export function GradeReviewList({ items, subjectId, now }: GradeReviewListProps) {
  const { t } = useTranslation('gradeReview');

  return (
    <ul aria-label={t('queue.listLabel')} className="flex flex-col gap-2">
      {items.map((item) => (
        <GradeReviewListItem key={item.id} item={item} subjectId={subjectId} now={now} />
      ))}
    </ul>
  );
}
