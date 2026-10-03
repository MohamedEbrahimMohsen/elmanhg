import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { pillTabClassName } from '@/shared/ui/pillTab';
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
            <Link to={tab.to} params={{ lessonId }} activeOptions={{ exact: true }} className={pillTabClassName}>
              {t(`lesson.tabs.${tab.key}`)}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
