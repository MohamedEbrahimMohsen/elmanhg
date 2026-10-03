import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { StudentProfileResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { UserActionButtons, type GrantablePlan } from './UserActionButtons';
import { UserStatusBadge } from './UserStatusBadge';

export interface StudentProfileCardProps {
  profile: StudentProfileResult;
  onSuspend: () => void;
  onReactivate: () => void;
  onGrant: (plan: GrantablePlan) => void;
}

function Entry({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <dt className="text-caption text-text-muted">{label}</dt>
      <dd className="text-ui text-text">{children}</dd>
    </div>
  );
}

export function StudentProfileCard({ profile, onSuspend, onReactivate, onGrant }: StudentProfileCardProps) {
  const { t, i18n } = useTranslation('users');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = (value: string) => formatDateTime(value, lng, 'date');
  const contact = profile.maskedPhone ?? profile.maskedEmail;

  return (
    <section className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <dl className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <Entry label={t('student.contact')}>
          {contact ? (
            <span dir="ltr" className="font-mono text-mono">
              {contact}
            </span>
          ) : null}
        </Entry>
        <Entry label={t('student.status')}>
          <UserStatusBadge status={profile.status} role="Student" />
        </Entry>
        <Entry label={t('student.joined')}>{date(profile.creationDate)}</Entry>
        {profile.onboardedAt ? <Entry label={t('student.onboarded')}>{date(profile.onboardedAt)}</Entry> : null}
        <Entry label={t('student.interests')}>
          {profile.subjectInterests.length > 0
            ? new Intl.ListFormat(lng, { type: 'unit' }).format(profile.subjectInterests)
            : t('student.noInterests')}
        </Entry>
        <Entry label={t('student.plan')}>
          {t(`plan.${profile.tier}`)}
          {profile.hasAskTeacher ? (
            <span className="ms-2 rounded-pill bg-accent-soft px-2.5 py-0.5 text-micro font-semibold text-accent">
              {t('plan.askTeacher')}
            </span>
          ) : null}
        </Entry>
      </dl>
      <UserActionButtons
        target={{ ...profile, role: 'Student' }}
        currentUserId={undefined}
        onSuspend={onSuspend}
        onReactivate={onReactivate}
        onGrant={onGrant}
      />
    </section>
  );
}
