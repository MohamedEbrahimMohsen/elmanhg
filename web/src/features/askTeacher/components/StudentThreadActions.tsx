import type { TeacherThreadResult } from '@/shared/api/generated/model';
import { FollowUpForm } from './FollowUpForm';
import { RatingPanel } from './RatingPanel';

export interface StudentThreadActionsProps {
  thread: TeacherThreadResult;
}

export function StudentThreadActions({ thread }: StudentThreadActionsProps) {
  return (
    <>
      {thread.canFollowUp ? <FollowUpForm threadId={thread.id} /> : null}
      {thread.canRate ? <RatingPanel threadId={thread.id} closes={thread.status === 'Answered'} /> : null}
    </>
  );
}
