import { useId, useReducer, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { useIsMutating } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { getSendAvatarMessageMutationKey, useGetAvatarStatus } from '@/shared/api/generated/avatar/avatar';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { registerAssistantLocales } from '../assistantLocales';
import { AssistantChat } from '../components/AssistantChat';
import { AssistantConversationList } from '../components/AssistantConversationList';
import { AssistantToolbar } from '../components/AssistantToolbar';
import { assistantReducer, globalContext, initialAssistantState } from '../hooks/assistantReducer';
import { AvatarControllerContext, type AvatarController } from '../hooks/avatarControllerContext';
import { useAssistantConversation } from '../hooks/useAssistantConversation';

registerAssistantLocales();

export interface AssistantPageProps {
  conversationId: string | undefined;
  page: number;
}

export function AssistantPage({ conversationId, page }: AssistantPageProps) {
  const { t } = useTranslation('avatar');
  const titleId = useId();
  const listId = useId();
  const listHeadingRef = useRef<HTMLHeadingElement>(null);
  const chatHeadingRef = useRef<HTMLHeadingElement>(null);
  const [state, dispatch] = useReducer(assistantReducer, initialAssistantState);
  const [mobileView, setMobileView] = useState<'chat' | 'list'>('chat');
  const status = useGetAvatarStatus();
  const examInProgress = status.data?.examInProgress === true;
  const isSending = useIsMutating({ mutationKey: getSendAvatarMessageMutationKey() }) > 0;
  const canLoad = status.isSuccess && !examInProgress;
  const opening = useAssistantConversation({ routeConversationId: conversationId, page, state, dispatch, canLoad });
  const controller: AvatarController = {
    state,
    dispatch,
    open: (context) => {
      dispatch({ type: 'newChat', context });
    },
    close: () => undefined,
  };
  const showView = (view: 'chat' | 'list') => {
    flushSync(() => {
      setMobileView(view);
    });
    (view === 'list' ? listHeadingRef : chatHeadingRef).current?.focus();
  };
  const showChat = () => {
    showView('chat');
  };
  const startNewChat = () => {
    dispatch({ type: 'newChat', context: globalContext });
    showChat();
  };

  return (
    <AvatarControllerContext value={controller}>
      <section aria-labelledby={titleId} className="flex h-assistant min-h-96 flex-col gap-4 lg:h-assistant-desktop">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 id={titleId} className="font-display text-h1 font-bold lg:text-h1-desktop">
            {t('panel.title')}
          </h1>
          {status.isSuccess && !examInProgress ? (
            <AssistantToolbar
              listId={listId}
              listOpen={mobileView === 'list'}
              newChatDisabled={isSending}
              onShowList={() => {
                showView('list');
              }}
              onNewChat={startNewChat}
            />
          ) : null}
        </div>
        {status.isPending ? (
          <div role="status" aria-busy="true" className="text-ui text-text-muted">
            {t('panel.loading')}
          </div>
        ) : status.isError ? (
          <div className="flex flex-col items-start gap-2">
            <p role="alert" className="text-ui text-danger">
              {t('panel.statusError')}
            </p>
            <Button variant="secondary" onClick={() => void status.refetch()}>
              {t('panel.retry')}
            </Button>
          </div>
        ) : (
          <div className="grid min-h-0 flex-1 grid-rows-1 gap-4 lg:grid-cols-3">
            {examInProgress ? null : (
              <AssistantConversationList
                id={listId}
                status={status.data}
                page={page}
                isSending={isSending}
                headingRef={listHeadingRef}
                className={mobileView === 'list' ? 'flex' : 'hidden lg:flex'}
                onBackToChat={showChat}
                onOpened={showChat}
              />
            )}
            <AssistantChat
              status={status.data}
              opening={opening}
              openingId={state.openingId}
              headingRef={chatHeadingRef}
              className={cn(
                examInProgress ? 'lg:col-span-3' : 'lg:col-span-2',
                examInProgress || mobileView === 'chat' ? 'flex' : 'hidden lg:flex',
              )}
              onNewChat={startNewChat}
            />
          </div>
        )}
      </section>
    </AvatarControllerContext>
  );
}
