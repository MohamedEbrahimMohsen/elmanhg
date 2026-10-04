import { Link } from '@tanstack/react-router';
import { Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { StudentAvatarConversationResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { Button } from '@/shared/ui/button';
import { conversationTitle } from '../api/avatarHistory';
import { pageSearch } from '../hooks/useAssistantConversation';

export interface AssistantConversationItemProps {
  conversation: StudentAvatarConversationResult;
  canDelete: boolean;
  page: number;
  isSending: boolean;
  onOpened: () => void;
  onDelete: (conversation: StudentAvatarConversationResult) => void;
}

export function AssistantConversationItem({
  conversation,
  canDelete,
  page,
  isSending,
  onOpened,
  onDelete,
}: AssistantConversationItemProps) {
  const { t, i18n } = useTranslation('avatar');
  const title = conversationTitle(conversation) ?? t('history.general');
  const date = formatDateTime(conversation.lastMessageAt, i18n.language);

  return (
    <li className="flex items-start gap-2 rounded-md border border-border bg-surface p-3 has-[[data-status=active]]:border-accent has-[[data-status=active]]:bg-accent-soft">
      <Link
        to="/student/assistant/$conversationId"
        params={{ conversationId: conversation.id }}
        search={pageSearch(page)}
        activeOptions={{ includeSearch: false }}
        aria-disabled={isSending || undefined}
        onClick={(event) => {
          if (isSending) {
            event.preventDefault();
            return;
          }
          onOpened();
        }}
        className="flex min-h-11 flex-1 flex-col items-start gap-1 rounded-sm text-start focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <span className="text-ui font-semibold">{title}</span>
        <span className="text-caption text-text-muted">
          {t('history.meta', { entryPoint: t(`history.entryPoint.${conversation.entryPoint}`), date })}
        </span>
        <span className="line-clamp-2 text-caption">{conversation.firstQuestion}</span>
      </Link>
      {canDelete ? (
        <Button
          variant="ghost"
          className="min-h-11 min-w-11 px-0 text-danger"
          aria-label={t('history.delete', { title })}
          onClick={() => {
            onDelete(conversation);
          }}
        >
          <Trash2 aria-hidden className="size-5" />
        </Button>
      ) : null}
    </li>
  );
}
