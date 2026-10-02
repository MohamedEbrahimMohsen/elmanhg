import { Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { StudentAvatarConversationResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';
import { conversationTitle } from '../api/avatarHistory';

export interface AvatarHistoryItemProps {
  conversation: StudentAvatarConversationResult;
  canDelete: boolean;
  isOpening: boolean;
  onOpen: (id: string) => void;
  onDelete: (conversation: StudentAvatarConversationResult) => void;
}

export function AvatarHistoryItem({ conversation, canDelete, isOpening, onOpen, onDelete }: AvatarHistoryItemProps) {
  const { t, i18n } = useTranslation('avatar');
  const title = conversationTitle(conversation) ?? t('history.general');
  const date = formatDate(new Date(conversation.lastMessageAt), i18n.language, 'arabic-indic', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <li className="flex items-start gap-2 rounded-md border border-border bg-surface p-3">
      <button
        type="button"
        className="flex min-h-11 flex-1 flex-col items-start gap-1 rounded-sm text-start focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        aria-busy={isOpening}
        onClick={() => {
          onOpen(conversation.id);
        }}
      >
        <span className="text-ui font-semibold">{title}</span>
        <span className="text-caption text-text-muted">
          {t('history.meta', { entryPoint: t(`history.entryPoint.${conversation.entryPoint}`), date })}
        </span>
        <span className="line-clamp-2 text-caption">{conversation.firstQuestion}</span>
      </button>
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
