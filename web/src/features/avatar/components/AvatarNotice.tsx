import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useGetAvatarStatus } from '@/shared/api/generated/avatar/avatar';
import type { AvatarNoticeKind } from '../api/avatarErrors';
import { useAvatar } from '../hooks/useAvatar';

export interface AvatarNoticeProps {
  kind: AvatarNoticeKind;
}

export function AvatarNotice({ kind }: AvatarNoticeProps) {
  const { t } = useTranslation('avatar');
  const { close } = useAvatar();
  const { data } = useGetAvatarStatus();
  const isFree = data?.tier === 'Free';
  const limit = data?.dailyMessageLimit ?? '';
  const text =
    kind === 'dailyLimit' ? t(isFree ? 'notice.dailyLimitFree' : 'notice.dailyLimit', { limit }) : t(`notice.${kind}`);

  return (
    <div
      role="status"
      className="flex max-w-full flex-col items-start gap-2 self-start rounded-md border border-warning bg-warning-soft px-3.5 py-2.5 text-ui"
    >
      <p>{text}</p>
      {kind === 'dailyLimit' && isFree ? (
        <Link
          to="/student/subscription"
          onClick={close}
          className="font-bold text-accent-text underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('notice.subscribe')}
        </Link>
      ) : null}
    </div>
  );
}
