import { useEffect, useRef, useState } from 'react';
import {
  mathDraftSaveDelayMilliseconds,
  mathDraftStorageKey,
  purgeExpiredMathDrafts,
  readMathDraft,
  writeMathDraft,
  type MathDraftOwner,
} from '../api/mathDraftStore';
import { emptyMathStepsValue, type MathStepsValue } from '../api/mathStepsValue';

export type MathDraftStatusValue = 'idle' | 'restored' | 'saved' | 'error';

export interface MathStepsDraftOptions {
  owner: MathDraftOwner;
  initialValue?: MathStepsValue | undefined;
  onChange?: ((value: MathStepsValue) => void) | undefined;
}

export interface MathStepsDraft {
  value: MathStepsValue;
  change: (value: MathStepsValue) => void;
  status: MathDraftStatusValue;
}

export function useMathStepsDraft({ owner, initialValue, onChange }: MathStepsDraftOptions): MathStepsDraft {
  const storageKey = mathDraftStorageKey(owner);
  const [initial] = useState(() => {
    const draft = readMathDraft(storageKey, Date.now());
    return { value: draft ?? initialValue ?? emptyMathStepsValue(), status: draft ? 'restored' : 'idle' } as const;
  });
  const [value, setValue] = useState(initial.value);
  const [status, setStatus] = useState<MathDraftStatusValue>(initial.status);
  const latest = useRef(initial.value);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    purgeExpiredMathDrafts(Date.now());
    const flush = () => {
      if (timer.current === undefined) {
        return;
      }
      clearTimeout(timer.current);
      timer.current = undefined;
      writeMathDraft(storageKey, latest.current, Date.now());
    };
    const onVisibility = () => {
      if (document.visibilityState === 'hidden') {
        flush();
      }
    };
    document.addEventListener('visibilitychange', onVisibility);
    window.addEventListener('pagehide', flush);
    return () => {
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('pagehide', flush);
      flush();
    };
  }, [storageKey]);

  const change = (next: MathStepsValue): void => {
    setValue(next);
    latest.current = next;
    onChange?.(next);
    if (timer.current !== undefined) {
      clearTimeout(timer.current);
    }
    timer.current = setTimeout(() => {
      timer.current = undefined;
      setStatus(writeMathDraft(storageKey, latest.current, Date.now()) ? 'saved' : 'error');
    }, mathDraftSaveDelayMilliseconds);
  };

  return { value, change, status };
}
