import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { lessonTabs } from '../api/lessonTabs';

export interface LessonTabsProps {
  lessonId: string;
}

export function LessonTabs({ lessonId }: LessonTabsProps) {
  const { t } = useTranslation('browse');

  return (
    <nav aria-label={t('lesson.tabsLabel')}>
      <ul className="flex flex-wrap gap-2">
        {lessonTabs.map((tab) => (
          <li key={tab.key}>
            <Link
              to={tab.to}
              params={{ lessonId }}
              activeOptions={{ exact: true }}
              activeProps={{ className: 'border-text bg-text text-surface' }}
              inactiveProps={{ className: 'border-border-strong bg-surface text-text hover:bg-soft' }}
              className="inline-flex min-h-11 items-center rounded-pill border px-3.5 text-micro font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
            >
              {t(`lesson.tabs.${tab.key}`)}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
