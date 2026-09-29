import { useTranslation } from 'react-i18next';
import type { TeacherInboxThreadResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { useClaimThread } from '../hooks/useClaimThread';
import { ReplyForm } from './ReplyForm';

export interface InboxThreadActionsProps {
  thread: TeacherInboxThreadResult;
}

function noteKey(thread: TeacherInboxThreadResult): string {
  if (!thread.isClaimedByMe && thread.teacherName) {
    return 'inboxThread.claimedByOther';
  }
  return thread.status === 'Closed' ? 'inboxThread.closed' : 'inboxThread.answered';
}

export function InboxThreadActions({ thread }: InboxThreadActionsProps) {
  const { t } = useTranslation('askTeacher');
  const { claim, isPending } = useClaimThread(thread.id);

  if (thread.canClaim) {
    return (
      <div>
        <Button onClick={claim} disabled={isPending}>
          {t('inboxThread.claim')}
        </Button>
      </div>
    );
  }
  if (thread.canReply) {
    return <ReplyForm threadId={thread.id} />;
  }
  return <p className="text-caption text-text-muted">{t(noteKey(thread))}</p>;
}
