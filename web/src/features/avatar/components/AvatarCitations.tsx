import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { AvatarCitationResult } from '@/shared/api/generated/model';
import { citationRoute } from '../api/citationLink';
import { useAvatar } from '../hooks/useAvatar';

export interface AvatarCitationsProps {
  citations: AvatarCitationResult[];
}

const chip = 'inline-flex rounded-pill bg-accent-soft px-2.5 py-0.5 text-micro text-accent';

export function AvatarCitations({ citations }: AvatarCitationsProps) {
  const { t } = useTranslation('avatar');
  const { close } = useAvatar();

  return (
    <div className="flex flex-col gap-1">
      <p className="text-caption font-semibold text-text-muted">{t('citations.label')}</p>
      <ul className="flex flex-wrap gap-1.5">
        {citations.map((citation) => {
          const section = t(`citations.section.${citation.section}`);
          const label = citation.sectionTitle ? `${section} — ${citation.sectionTitle}` : section;
          const route = citationRoute(citation);
          return (
            <li key={citation.reference}>
              {route ? (
                <Link
                  to={route.to}
                  params={route.params}
                  onClick={close}
                  className={`${chip} focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden`}
                >
                  {label}
                </Link>
              ) : (
                <span className={chip}>{label}</span>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
