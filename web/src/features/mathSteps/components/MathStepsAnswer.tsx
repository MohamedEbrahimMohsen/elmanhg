import { mathDraftStorageKey, type MathDraftOwner } from '../api/mathDraftStore';
import type { MathStepsValue } from '../api/mathStepsValue';
import { MathStepsDraftEditor } from './MathStepsDraftEditor';

export interface MathStepsAnswerProps {
  owner: MathDraftOwner;
  initialValue?: MathStepsValue | undefined;
  onChange?: ((value: MathStepsValue) => void) | undefined;
  disabled?: boolean | undefined;
}

export function MathStepsAnswer(props: MathStepsAnswerProps) {
  return <MathStepsDraftEditor key={mathDraftStorageKey(props.owner)} {...props} />;
}
