import { useId } from 'react';
import { ClipboardCheck, InfinityIcon, MessageCircleQuestionMark, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const valueKeys = ['practice', 'exams', 'askTeacher'] as const;

type ValueKey = (typeof valueKeys)[number];

const valueIcons: Record<ValueKey, LucideIcon> = {
  practice: InfinityIcon,
  exams: ClipboardCheck,
  askTeacher: MessageCircleQuestionMark,
};

export interface ValuePropsProps {
  replySlaHours: number | undefined;
}

export function ValueProps({ replySlaHours }: ValuePropsProps) {
  const { t } = useTranslation('landing');
  const headingId = useId();
  const body = (key: ValueKey) => {
    if (key !== 'askTeacher') {
      return t(`values.${key}.body`);
    }
    return replySlaHours === undefined
      ? t('values.askTeacher.body')
      : t('values.askTeacher.bodyWithHours', { hours: replySlaHours });
  };

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('values.title')}
      </h2>
      <ul className="grid grid-cols-1 gap-3 md:grid-cols-3">
        {valueKeys.map((key) => {
          const Icon = valueIcons[key];
          return (
            <li
              key={key}
              className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
            >
              <Icon aria-hidden strokeWidth={1.8} className="size-6 text-accent" />
              <h3 className="font-display text-h3 font-bold">{t(`values.${key}.title`)}</h3>
              <p className="text-caption text-text-muted">{body(key)}</p>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
