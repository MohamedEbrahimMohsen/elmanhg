import { useMathStepsDraft } from '../hooks/useMathStepsDraft';
import { MathDraftStatus } from './MathDraftStatus';
import type { MathStepsAnswerProps } from './MathStepsAnswer';
import { MathStepsInput } from './MathStepsInput';

export function MathStepsDraftEditor({ owner, initialValue, onChange, disabled }: MathStepsAnswerProps) {
  const draft = useMathStepsDraft({ owner, initialValue, onChange });

  return (
    <div className="flex flex-col gap-3">
      <MathStepsInput value={draft.value} onChange={draft.change} disabled={disabled} />
      <MathDraftStatus status={draft.status} />
    </div>
  );
}
