import { useTranslation } from 'react-i18next';
import type { TypeShortfall } from '../api/blueprintValues';

export interface ShortfallNoticeProps {
  title: string;
  shortfalls: readonly TypeShortfall[];
}

export function ShortfallNotice({ title, shortfalls }: ShortfallNoticeProps) {
  const { t } = useTranslation('blueprints');

  if (shortfalls.length === 0) {
    return null;
  }

  return (
    <div
      aria-live="polite"
      className="rounded-md border border-warning bg-warning-soft px-3.5 py-3 text-caption text-text"
    >
      <p className="font-bold">{title}</p>
      {shortfalls.map(({ type, required, available, missing }) => (
        <p key={type}>
          {t('editor.shortfallLine', { type: t(`questions:types.${type}`), required, available, missing })}
        </p>
      ))}
    </div>
  );
}
