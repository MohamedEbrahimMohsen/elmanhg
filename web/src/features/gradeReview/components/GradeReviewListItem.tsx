import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { stemExcerpt } from '@/features/questions';
import type { GradeReviewItemResult } from '@/shared/api/generated/model';
import { formatRelativeTime } from '@/shared/lib/dateTime';
import { formatNumber } from '@/shared/lib/format';
import { kindSegments, toKind } from '../api/gradeReviewOptions';

export interface GradeReviewListItemProps {
  item: GradeReviewItemResult;
  subjectId: string;
  now: Date;
}

const chipClassName = 'shrink-0 rounded-full bg-accent-soft px-2.5 py-0.5 text-micro font-bold text-accent-text';

export function GradeReviewListItem({ item, subjectId, now }: GradeReviewListItemProps) {
  const { t, i18n } = useTranslation('gradeReview');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const meta = [
    `${item.unitName} › ${item.lessonName}`,
    t(`reasons.${item.reviewReason}`, { defaultValue: item.reviewReason }),
    t('queue.age', { age: formatRelativeTime(item.requestedAt, now, lng) }),
  ];
  const score =
    item.aiScore === null
      ? t('queue.noAiScore')
      : t('queue.aiScore', {
          score: formatNumber(Number(item.aiScore), lng),
          maxScore: formatNumber(Number(item.maxScore), lng),
        });

  return (
    <li className="flex items-start gap-3 rounded-md border border-border bg-surface px-3.5 py-3">
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <Link
          to="/teacher/grade/$subjectId/$kind/$gradeId"
          params={{ subjectId, kind: kindSegments[toKind(item.kind)], gradeId: item.id }}
          className="text-ui font-bold text-text hover:text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {stemExcerpt(item.stem)}
        </Link>
        <p className="text-caption text-text-muted">{meta.join(' · ')}</p>
      </div>
      <div className="flex shrink-0 flex-col items-end gap-1">
        <span className={chipClassName}>{score}</span>
        {item.confidence === null ? null : (
          <span className="text-caption text-text-muted">
            {t('queue.confidence', {
              percent: formatNumber(Math.round(Number(item.confidence) * 100), lng),
            })}
          </span>
        )}
      </div>
    </li>
  );
}
