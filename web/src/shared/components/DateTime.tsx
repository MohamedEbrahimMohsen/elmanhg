import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime, formatRelativeTime, isRecent, toDate, type DateTimeStyle } from '@/shared/lib/dateTime';

export interface DateTimeProps {
  value: Date | string;
  style?: DateTimeStyle;
  relative?: boolean;
}

export function DateTime({ value, style = 'dateTime', relative = false }: DateTimeProps) {
  const { i18n } = useTranslation();
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const [now] = useState(() => new Date());
  const full = formatDateTime(value, lng, style);
  const recent = relative && isRecent(value, now);

  return (
    <time dateTime={toDate(value).toISOString()} title={recent ? full : undefined}>
      {recent ? formatRelativeTime(value, now, lng) : full}
    </time>
  );
}
