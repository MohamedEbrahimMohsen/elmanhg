import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

export interface StudentBreadcrumbsProps {
  subject?: { id: string; name: string };
  unit?: { id: string; name: string };
  current: string;
}

const itemClassName = "min-w-0 break-words before:me-2 before:content-['/']";
const linkClassName =
  'rounded-sm text-accent-text underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

export function StudentBreadcrumbs({ subject, unit, current }: StudentBreadcrumbsProps) {
  const { t } = useTranslation('browse');

  return (
    <nav aria-label={t('breadcrumb.label')}>
      <ol className="flex flex-wrap gap-2 text-caption text-text-muted">
        <li>
          <Link to="/student" className={linkClassName}>
            {t('breadcrumb.home')}
          </Link>
        </li>
        {subject ? (
          <li className={itemClassName}>
            <Link to="/student/subject/$subjectId" params={{ subjectId: subject.id }} className={linkClassName}>
              {subject.name}
            </Link>
          </li>
        ) : null}
        {unit ? (
          <li className={itemClassName}>
            <Link to="/student/unit/$unitId" params={{ unitId: unit.id }} className={linkClassName}>
              {unit.name}
            </Link>
          </li>
        ) : null}
        <li aria-current="page" className={itemClassName}>
          {current}
        </li>
      </ol>
    </nav>
  );
}
