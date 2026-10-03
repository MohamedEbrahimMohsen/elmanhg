import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TeacherThreadSummaryResult } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { remainingHours } from '../api/remainingHours';
import { threadBadge, type ThreadBadge } from '../api/threadBadge';

export interface ThreadStatusBadgeProps {
  thread: Pick<TeacherThreadSummaryResult, 'status' | 'isOverdue' | 'slaDueAt'>;
}

const badgeClasses: Record<ThreadBadge, string> = {
  closed: 'bg-soft text-text-muted',
  overdue: 'bg-danger-soft text-danger',
  awaiting: 'bg-soft text-text-muted',
  answered: 'bg-success-soft text-success-text',
};

export function ThreadStatusBadge({ thread }: ThreadStatusBadgeProps) {
  const { t } = useTranslation('askTeacher');
  const [now] = useState(() => new Date());
  const badge = threadBadge(thread);

  return (
    <span className={cn('inline-flex rounded-pill px-2.5 py-0.5 text-micro font-semibold', badgeClasses[badge])}>
      {t(`badge.${badge}`, { hours: remainingHours(thread.slaDueAt, now) })}
    </span>
  );
}
