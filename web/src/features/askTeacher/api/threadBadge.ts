import type { TeacherThreadSummaryResult } from '@/shared/api/generated/model';

export type ThreadBadge = 'closed' | 'overdue' | 'awaiting' | 'answered';

export function threadBadge(thread: Pick<TeacherThreadSummaryResult, 'status' | 'isOverdue'>): ThreadBadge {
  if (thread.status === 'Closed') {
    return 'closed';
  }
  if (thread.isOverdue) {
    return 'overdue';
  }
  return thread.status === 'Open' ? 'awaiting' : 'answered';
}
