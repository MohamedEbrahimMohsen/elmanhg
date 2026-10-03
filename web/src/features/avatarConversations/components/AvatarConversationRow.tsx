import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { AdminAvatarConversationResult } from '@/shared/api/generated/model';
import { DateTime } from '@/shared/components/DateTime';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
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
        <DateTime value={item.lastMessageAt} relative />
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
        <Button asChild variant="secondary" size="sm">
          <Link
            to="/admin/avatar-conversation/$conversationId"
            params={{ conversationId: item.id }}
            aria-label={t('table.openLabel', { student: item.studentName })}
          >
            {t('table.open')}
          </Link>
        </Button>
      </td>
    </tr>
  );
}
