import { useEffect, useId, useState, type Ref } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { ArrowLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { AvatarStatusResult, StudentAvatarConversationResult } from '@/shared/api/generated/model';
import { Pagination } from '@/shared/components/Pagination';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { useAvatarHistory } from '../hooks/useAvatarHistory';
import { useDeleteAvatarConversation } from '../hooks/useDeleteAvatarConversation';
import { AssistantConversationItem } from './AssistantConversationItem';
import { DeleteAvatarConversationDialog } from './DeleteAvatarConversationDialog';

export interface AssistantConversationListProps {
  id: string;
  status: AvatarStatusResult;
  page: number;
  isSending: boolean;
  headingRef: Ref<HTMLHeadingElement>;
  className: string;
  onBackToChat: () => void;
  onOpened: () => void;
}

export function AssistantConversationList(props: AssistantConversationListProps) {
  const { id, status, page, isSending, headingRef, className, onBackToChat, onOpened } = props;
  const { t } = useTranslation('avatar');
  const headingId = useId();
  const { list } = useAvatarHistory(page);
  const { remove, isPending } = useDeleteAvatarConversation();
  const [pendingDelete, setPendingDelete] = useState<StudentAvatarConversationResult | null>(null);
  const navigate = useNavigate();
  const items = list.data?.items ?? [];
  const totalPages = Number(list.data?.totalPages ?? 0);
  const goToPage = (next: number) => {
    void navigate({ to: '.', search: (prev) => ({ ...prev, page: next > 1 ? next : undefined }) });
  };
  const emptyPastFirst = list.isSuccess && items.length === 0 && page > 1;

  useEffect(() => {
    if (emptyPastFirst) {
      void navigate({ to: '.', search: (prev) => ({ ...prev, page: page > 2 ? page - 1 : undefined }) });
    }
  }, [emptyPastFirst, page, navigate]);

  return (
    <section
      id={id}
      aria-labelledby={headingId}
      className={cn(
        'min-h-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:col-span-1',
        className,
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <h2
          id={headingId}
          ref={headingRef}
          tabIndex={-1}
          className="font-display text-h3 font-bold focus-visible:outline-hidden"
        >
          {t('history.title')}
        </h2>
        <Button variant="ghost" className="lg:hidden" onClick={onBackToChat}>
          <ArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
          {t('panel.backToChat')}
        </Button>
      </div>
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
          <ul className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto">
            {items.map((conversation) => (
              <AssistantConversationItem
                key={conversation.id}
                conversation={conversation}
                canDelete={status.conversationDeletionEnabled}
                page={page}
                isSending={isSending}
                onOpened={onOpened}
                onDelete={setPendingDelete}
              />
            ))}
          </ul>
          {totalPages > 1 ? <Pagination page={page} totalPages={totalPages} onPageChange={goToPage} /> : null}
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
