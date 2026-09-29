import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { AdminAvatarConversationResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { formatCount } from '../api/avatarConversationFormat';

export interface AvatarConversationRowProps {
  item: AdminAvatarConversationResult;
}

const cellClassName = 'px-2.5 py-2.25 align-top text-caption';

export function AvatarConversationRow({ item }: AvatarConversationRowProps) {
  const { t, i18n } = useTranslation('avatarConversations');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const scope = [item.subjectName, item.lessonName].filter((name) => name !== null).join(' › ');

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cn(cellClassName, 'whitespace-nowrap')}>
        {formatDate(new Date(item.lastMessageAt), lng, 'latin', { dateStyle: 'medium', timeStyle: 'short' })}
      </td>
      <td className={cellClassName}>{item.studentName}</td>
      <td className={cellClassName}>
        <span className="block">{t(`entryPoint.${item.entryPoint}`)}</span>
        {scope === '' ? null : <span className="block text-text-muted">{scope}</span>}
      </td>
      <td className={cellClassName}>
        <span className="line-clamp-2">{item.firstQuestion}</span>
      </td>
      <td className={cellClassName}>{formatCount(Number(item.messageCount), lng)}</td>
      <td className={cellClassName}>
        <Link
          to="/admin/avatar-conversation/$conversationId"
          params={{ conversationId: item.id }}
          aria-label={t('table.openLabel', { student: item.studentName })}
          className="rounded-sm text-accent underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('table.open')}
        </Link>
      </td>
    </tr>
  );
}
