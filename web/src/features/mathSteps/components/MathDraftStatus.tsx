import { useTranslation } from 'react-i18next';
import type { MathDraftStatusValue } from '../hooks/useMathStepsDraft';

export interface MathDraftStatusProps {
  status: MathDraftStatusValue;
}

export function MathDraftStatus({ status }: MathDraftStatusProps) {
  const { t } = useTranslation('mathSteps');

  return (
    <p role="status" className={status === 'error' ? 'text-caption text-danger' : 'text-caption text-text-muted'}>
      {status === 'idle' ? '' : t(`draft.${status}`)}
    </p>
  );
}
