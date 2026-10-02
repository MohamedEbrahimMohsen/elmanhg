import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { UserSummaryResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import type { UserListTab } from '../schemas/usersSearchSchema';
import { TeacherSubjectsCell } from './TeacherSubjectsCell';
import { UserActionButtons, type GrantablePlan } from './UserActionButtons';
import { UserStatusBadge } from './UserStatusBadge';

export interface UserListRowProps {
  tab: UserListTab;
  item: UserSummaryResult;
  currentUserId: string | undefined;
  onSuspend: (item: UserSummaryResult) => void;
  onReactivate: (item: UserSummaryResult) => void;
  onGrant: (item: UserSummaryResult, plan: GrantablePlan) => void;
  onEditPhone: (item: UserSummaryResult) => void;
}

const cellClassName = 'px-2.5 py-2.25 align-top text-caption';

export function UserListRow({
  tab,
  item,
  currentUserId,
  onSuspend,
  onReactivate,
  onGrant,
  onEditPhone,
}: UserListRowProps) {
  const { t, i18n } = useTranslation('users');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const contact = item.maskedPhone ?? item.maskedEmail;

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cn(cellClassName, 'font-semibold')}>
        {tab === 'students' ? (
          <Link
            to="/admin/student/$studentId"
            params={{ studentId: item.id }}
            className="rounded-sm text-accent hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          >
            {item.displayName}
          </Link>
        ) : (
          item.displayName
        )}
      </td>
      <td className={cellClassName}>
        {contact ? (
          <span dir="ltr" className="font-mono text-mono">
            {contact}
          </span>
        ) : null}
      </td>
      {tab === 'students' ? (
        <td className={cellClassName}>
          {t(`plan.${item.tier ?? 'Free'}`)}
          {item.hasAskTeacher ? (
            <span className="ms-2 rounded-pill bg-accent-soft px-2.5 py-0.5 text-micro font-semibold text-accent">
              {t('plan.askTeacher')}
            </span>
          ) : null}
        </td>
      ) : null}
      {tab === 'teachers' ? (
        <td className={cellClassName}>
          <TeacherSubjectsCell teacherId={item.id} teacherName={item.displayName} subjectIds={item.subjectIds} />
        </td>
      ) : null}
      <td className={cellClassName}>
        <div className="flex flex-col items-start gap-1.5">
          <UserStatusBadge status={item.status} role={item.role} />
          {item.invitationPending ? (
            <span className="inline-flex rounded-pill bg-warning-soft px-2.5 py-0.5 text-micro font-semibold text-warning">
              {t('status.pendingInvite')}
            </span>
          ) : null}
        </div>
      </td>
      {tab === 'students' ? (
        <td className={cn(cellClassName, 'whitespace-nowrap')}>
          {formatDate(new Date(item.creationDate), lng, 'latin', { dateStyle: 'medium' })}
        </td>
      ) : null}
      <td className={cellClassName}>
        <div className="flex flex-wrap items-start gap-2">
          <UserActionButtons
            target={item}
            currentUserId={currentUserId}
            onSuspend={() => {
              onSuspend(item);
            }}
            onReactivate={() => {
              onReactivate(item);
            }}
            onGrant={(plan) => {
              onGrant(item, plan);
            }}
          />
          {tab === 'teachers' ? (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                onEditPhone(item);
              }}
            >
              {t('teacherPhone.edit')}
            </Button>
          ) : null}
        </div>
      </td>
    </tr>
  );
}
