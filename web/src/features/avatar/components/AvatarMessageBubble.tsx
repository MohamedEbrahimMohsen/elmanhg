import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { AvatarMessage } from '../hooks/avatarReducer';
import { AvatarCitations } from './AvatarCitations';

export interface AvatarMessageBubbleProps {
  message: Exclude<AvatarMessage, { kind: 'notice' }>;
}

export function AvatarMessageBubble({ message }: AvatarMessageBubbleProps) {
  const { t } = useTranslation('avatar');

  if (message.kind === 'student') {
    return (
      <div className="max-w-full self-end rounded-md bg-soft px-3.5 py-2.5 text-ui">
        <span className="sr-only">{t('you')}: </span>
        <span className="whitespace-pre-line">{message.text}</span>
      </div>
    );
  }
  return (
    <div className="flex max-w-full flex-col gap-2 self-start rounded-md border border-border bg-surface px-3.5 py-2.5 text-ui">
      <p className="flex gap-2">
        <Sparkles aria-hidden strokeWidth={1.8} className="mt-1 size-3.5 shrink-0 text-accent-text" />
        <span className="sr-only">{t('assistant')}: </span>
        <span className="whitespace-pre-line">{message.text}</span>
      </p>
      {message.citations.length > 0 ? <AvatarCitations citations={message.citations} /> : null}
    </div>
  );
}
