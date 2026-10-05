import { useId, type Ref } from 'react';
import { useTranslation } from 'react-i18next';
import type { AvatarStatusResult } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import type { AssistantOpening } from '../hooks/useAssistantConversation';
import { useAvatar } from '../hooks/useAvatar';
import { AssistantComposer } from './AssistantComposer';
import { AssistantContextPicker } from './AssistantContextPicker';
import { AvatarConversation } from './AvatarConversation';

export interface AssistantChatProps {
  status: AvatarStatusResult;
  opening: AssistantOpening;
  openingId: string | null;
  headingRef: Ref<HTMLHeadingElement>;
  className: string;
  onNewChat: () => void;
}

export function AssistantChat({ status, opening, openingId, headingRef, className, onNewChat }: AssistantChatProps) {
  const { t } = useTranslation();
  const headingId = useId();
  const { state, open } = useAvatar();
  const fresh = state.conversationId === null && openingId === null && state.messages.length === 0;

  return (
    <section
      aria-labelledby={headingId}
      className={cn('min-h-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5', className)}
    >
      <h2 id={headingId} ref={headingRef} tabIndex={-1} className="sr-only">
        {t('avatar:panel.messages')}
      </h2>
      {opening.phase === 'loading' ? (
        <div role="status" aria-busy="true" className="text-ui text-text-muted">
          {t('assistant:open.loading')}
        </div>
      ) : opening.phase === 'notFound' ? (
        <div className="flex flex-col items-start gap-2">
          <p role="alert" className="text-ui text-danger">
            {t('assistant:open.notFound')}
          </p>
          <Button variant="primary" onClick={onNewChat}>
            {t('assistant:newChat')}
          </Button>
        </div>
      ) : opening.phase === 'error' ? (
        <div className="flex flex-col items-start gap-2">
          <p role="alert" className="text-ui text-danger">
            {t('assistant:open.error')}
          </p>
          <Button variant="secondary" onClick={opening.retry}>
            {t('avatar:panel.retry')}
          </Button>
        </div>
      ) : (
        <>
          {!status.examInProgress && fresh ? (
            <AssistantContextPicker
              context={state.context}
              onChange={(context) => {
                open(context);
              }}
            />
          ) : (
            <p className="text-caption text-text-muted">
              {t('avatar:panel.context', { title: state.context.title ?? t('avatar:panel.contextGlobal') })}
            </p>
          )}
          {status.tier === 'Free' ? (
            <p className="text-caption text-text-muted">
              {t('avatar:panel.quota', { used: status.messagesUsedToday, limit: status.dailyMessageLimit })}
            </p>
          ) : null}
          <AvatarConversation status={status} />
          <AssistantComposer status={status} />
        </>
      )}
    </section>
  );
}
