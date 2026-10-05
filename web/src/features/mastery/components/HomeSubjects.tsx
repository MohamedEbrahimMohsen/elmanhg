import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { SubjectMasteryResult } from '@/shared/api/generated/model';
import { SubjectMasteryCard } from './SubjectMasteryCard';

export interface HomeSubjectsProps {
  subjects: SubjectMasteryResult[];
}

export function HomeSubjects({ subjects }: HomeSubjectsProps) {
  const { t } = useTranslation('mastery');
  const chosen = subjects.filter((subject) => subject.isInterested);
  const others = subjects.filter((subject) => !subject.isInterested);
  const primary = chosen.length > 0 ? chosen : subjects;
  const grid = (items: SubjectMasteryResult[]) => (
    <ul className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
      {items.map((subject) => (
        <li key={subject.subjectId}>
          <SubjectMasteryCard subject={subject} />
        </li>
      ))}
    </ul>
  );

  return (
    <>
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('subjects.title')}</h2>
        <Link
          to="/onboarding"
          className="inline-flex min-h-11 items-center rounded-sm text-ui font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('subjects.edit')}
        </Link>
      </div>
      {subjects.length === 0 ? <p className="text-ui text-text-muted">{t('subjects.empty')}</p> : grid(primary)}
      {chosen.length > 0 && others.length > 0 ? (
        <>
          <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('subjects.others')}</h2>
          {grid(others)}
        </>
      ) : null}
    </>
  );
}
