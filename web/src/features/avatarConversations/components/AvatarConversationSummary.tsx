import { useTranslation } from 'react-i18next';
import type { AdminAvatarConversationDetailResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { formatCostUsd, formatCount } from '../api/avatarConversationFormat';

export interface AvatarConversationSummaryProps {
  conversation: AdminAvatarConversationDetailResult;
}

export function AvatarConversationSummary({ conversation }: AvatarConversationSummaryProps) {
  const { t, i18n } = useTranslation('avatarConversations');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = (value: string) => formatDateTime(value, lng);
  const scope = [conversation.subjectName, conversation.unitName, conversation.lessonName]
    .filter((name) => name !== null)
    .join(' › ');
  const rows = [
    { key: 'student', value: conversation.studentName },
    {
      key: 'context',
      value:
        scope === ''
          ? t(`entryPoint.${conversation.entryPoint}`)
          : `${t(`entryPoint.${conversation.entryPoint}`)} — ${scope}`,
    },
    { key: 'startedAt', value: date(conversation.startedAt) },
    { key: 'lastMessageAt', value: date(conversation.lastMessageAt) },
    { key: 'messages', value: formatCount(Number(conversation.messageCount), lng) },
    {
      key: 'cost',
      value:
        conversation.totalCostUsd === null
          ? t('detail.notPriced')
          : formatCostUsd(Number(conversation.totalCostUsd), lng),
    },
  ];

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
      <dl className="grid grid-cols-1 gap-3 md:grid-cols-2">
        {rows.map((row) => (
          <div key={row.key} className="flex flex-col gap-0.5">
            <dt className="text-caption text-text-muted">{t(`detail.${row.key}`)}</dt>
            <dd className="text-ui text-text">{row.value}</dd>
          </div>
        ))}
      </dl>
      <p className="text-caption text-text-muted">
        {t('detail.tokens', {
          input: formatCount(Number(conversation.totalInputTokens), lng),
          output: formatCount(Number(conversation.totalOutputTokens), lng),
        })}
      </p>
    </div>
  );
}
