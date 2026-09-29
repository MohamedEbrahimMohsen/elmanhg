import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { AdminAvatarMessageResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { formatCostUsd, formatCount } from '../api/avatarConversationFormat';
import { AvatarLoggedContext } from './AvatarLoggedContext';

export interface AvatarLoggedMessageProps {
  message: AdminAvatarMessageResult;
}

const technicalClassName = 'font-mono text-mono';

export function AvatarLoggedMessage({ message }: AvatarLoggedMessageProps) {
  const { t, i18n } = useTranslation('avatarConversations');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const time = formatDate(new Date(message.createdAt), lng, 'latin', { timeStyle: 'short' });

  if (message.role === 'Student') {
    return (
      <article className="flex flex-col gap-1 rounded-md bg-soft p-3">
        <p className="text-caption text-text-muted">
          {t('message.student')} · {time}
        </p>
        <p className="text-ui whitespace-pre-wrap text-text">{message.text}</p>
      </article>
    );
  }

  const count = (value: number | string | null) => (value === null ? '—' : formatCount(Number(value), lng));
  const meta = [
    { key: 'model', value: message.model ?? '—' },
    { key: 'promptVersion', value: message.promptVersion ?? '—' },
    { key: 'tokens', value: `${count(message.inputTokens)} / ${count(message.outputTokens)}` },
    { key: 'cost', value: message.costUsd === null ? '—' : formatCostUsd(Number(message.costUsd), lng) },
    { key: 'stopReason', value: message.stopReason ?? '—' },
    { key: 'history', value: count(message.historyMessageCount) },
  ];

  return (
    <article className="flex flex-col gap-3 rounded-md border border-border bg-surface p-3">
      <p className="flex items-center gap-2 text-caption text-text-muted">
        <Sparkles aria-hidden strokeWidth={1.8} className="size-3.5 shrink-0 text-accent" />
        {t('message.assistant')} · {time}
      </p>
      <p className="text-ui whitespace-pre-wrap text-text">{message.text}</p>
      <dl className="flex flex-wrap gap-x-4 gap-y-1 text-caption">
        {meta.map((item) => (
          <div key={item.key} className="flex gap-1">
            <dt className="text-text-muted">{t(`message.${item.key}`)}:</dt>
            <dd>
              <bdi dir="ltr" className={technicalClassName}>
                {item.value}
              </bdi>
            </dd>
          </div>
        ))}
      </dl>
      {message.citations.length > 0 ? (
        <div className="flex flex-col gap-1.5">
          <p className="text-caption text-text-muted">{t('message.sources')}</p>
          <ul className="flex flex-wrap gap-2">
            {message.citations.map((citation) => (
              <li key={citation.reference} className="rounded-full bg-soft px-2.5 py-1 text-caption text-text">
                {citation.sectionTitle
                  ? `${t(`section.${citation.section}`)} — ${citation.sectionTitle}`
                  : t(`section.${citation.section}`)}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      {message.context ? <AvatarLoggedContext context={message.context} /> : null}
    </article>
  );
}
