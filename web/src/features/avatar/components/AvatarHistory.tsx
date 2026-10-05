import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AvatarStatusResult, StudentAvatarConversationResult } from '@/shared/api/generated/model';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/ui/button';
import { useAvatarHistory } from '../hooks/useAvatarHistory';
import { useDeleteAvatarConversation } from '../hooks/useDeleteAvatarConversation';
import { AvatarHistoryItem } from './AvatarHistoryItem';
import { DeleteAvatarConversationDialog } from './DeleteAvatarConversationDialog';

export interface AvatarHistoryProps {
  status: AvatarStatusResult;
}

export function AvatarHistory({ status }: AvatarHistoryProps) {
  const { t } = useTranslation('avatar');
  const headingId = useId();
  const [page, setPage] = useState(1);
  const [pendingDelete, setPendingDelete] = useState<StudentAvatarConversationResult | null>(null);
  const { list, resume, openingId } = useAvatarHistory(page);
  const { remove, isPending } = useDeleteAvatarConversation();
  const canDelete = status.conversationDeletionEnabled;
  const items = list.data?.items ?? [];
  const totalPages = Number(list.data?.totalPages ?? 0);

  if (list.isSuccess && items.length === 0 && page > 1) {
    setPage(page - 1);
  }

  return (
    <section aria-labelledby={headingId} className="flex flex-1 flex-col gap-3 overflow-hidden">
      <h3 id={headingId} className="font-display text-h3 font-bold">
        {t('history.title')}
      </h3>
      {list.isPending ? (
        <div role="status" aria-busy="true" className="text-ui text-text-muted">
          {t('history.loading')}
        </div>
      ) : list.isError ? (
        <div className="flex flex-col items-start gap-2">
          <p role="alert" className="text-ui text-danger">
            {t('history.error')}
          </p>
          <Button
            variant="secondary"
            onClick={() => {
              void list.refetch();
            }}
          >
            {t('panel.retry')}
          </Button>
        </div>
      ) : items.length === 0 ? (
        <p className="text-ui text-text-muted">{t('history.empty')}</p>
      ) : (
        <>
          <ul className="flex flex-1 flex-col gap-2 overflow-y-auto">
            {items.map((conversation) => (
              <AvatarHistoryItem
                key={conversation.id}
                conversation={conversation}
                canDelete={canDelete}
                isOpening={openingId === conversation.id}
                onOpen={(id) => {
                  void resume(id);
                }}
                onDelete={setPendingDelete}
              />
            ))}
          </ul>
          {totalPages > 1 ? <Pagination page={page} totalPages={totalPages} onPageChange={setPage} /> : null}
        </>
      )}
      <DeleteAvatarConversationDialog
        conversation={pendingDelete}
        isPending={isPending}
        onOpenChange={(open) => {
          if (!open) setPendingDelete(null);
        }}
        onConfirm={() => (pendingDelete === null ? Promise.resolve() : remove(pendingDelete.id))}
      />
    </section>
  );
}
