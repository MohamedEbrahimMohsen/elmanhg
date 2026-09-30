import { useTranslation } from 'react-i18next';
import type { EssayDraftStatusValue } from '../hooks/useEssayDraft';

export interface EssayDraftStatusProps {
  status: EssayDraftStatusValue;
}

export function EssayDraftStatus({ status }: EssayDraftStatusProps) {
  const { t } = useTranslation('quiz');

  return (
    <p role="status" className={status === 'error' ? 'text-caption text-danger' : 'text-caption text-text-muted'}>
      {status === 'idle' ? '' : t(`essayDraft.${status}`)}
    </p>
  );
}
