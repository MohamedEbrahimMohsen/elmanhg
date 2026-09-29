import { useEffect, useRef } from 'react';
import { useIsMutating } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { getSendAvatarMessageMutationKey } from '@/shared/api/generated/avatar/avatar';
import type { AvatarStatusResult } from '@/shared/api/generated/model';
import { useAvatar } from '../hooks/useAvatar';
import { AvatarMessageBubble } from './AvatarMessageBubble';
import { AvatarNotice } from './AvatarNotice';

export interface AvatarConversationProps {
  status: AvatarStatusResult;
}

export function AvatarConversation({ status }: AvatarConversationProps) {
  const { t } = useTranslation('avatar');
  const { state } = useAvatar();
  const isSending = useIsMutating({ mutationKey: getSendAvatarMessageMutationKey() }) > 0;
  const logRef = useRef<HTMLDivElement>(null);
  const count = state.messages.length;
  const { entryPoint, title } = state.context;
  const greeting =
    entryPoint === 'Lesson'
      ? t('greeting.lesson', { title: title ?? '' })
      : entryPoint === 'Global'
        ? t('greeting.global')
        : t('greeting.question');

  useEffect(() => {
    const log = logRef.current;
    if (log) log.scrollTop = log.scrollHeight;
  }, [count]);

  return (
    <div
      ref={logRef}
      role="log"
      aria-live="polite"
      aria-label={t('panel.messages')}
      className="flex flex-1 flex-col gap-2.5 overflow-y-auto"
    >
      <AvatarMessageBubble message={{ id: 'greeting', kind: 'assistant', text: greeting, citations: [] }} />
      {state.messages.map((message) =>
        message.kind === 'notice' ? (
          <AvatarNotice key={message.id} kind={message.notice} />
        ) : (
          <AvatarMessageBubble key={message.id} message={message} />
        ),
      )}
      {status.examInProgress ? (
        <AvatarNotice kind="examInProgress" />
      ) : Number(status.messagesRemainingToday) === 0 ? (
        <AvatarNotice kind="dailyLimit" />
      ) : null}
      {isSending ? (
        <p role="status" className="text-caption text-text-muted">
          {t('panel.typing')}
        </p>
      ) : null}
    </div>
  );
}
