import { useEffect, useRef, useState } from 'react';
import {
  clearEssayDraft,
  essayDraftSaveDelayMilliseconds,
  essayDraftStorageKey,
  purgeExpiredEssayDrafts,
  readEssayDraft,
  writeEssayDraft,
  type EssayDraftOwner,
} from '../api/essayDraftStore';

export type EssayDraftStatusValue = 'idle' | 'restored' | 'saved' | 'error';

export interface EssayDraft {
  text: string;
  change: (text: string) => void;
  status: EssayDraftStatusValue;
  discard: () => void;
}

export function useEssayDraft(owner: EssayDraftOwner): EssayDraft {
  const storageKey = essayDraftStorageKey(owner);
  const [initial] = useState(() => {
    const draft = readEssayDraft(storageKey, Date.now());
    return { text: draft ?? '', status: draft === null ? 'idle' : 'restored' } as const;
  });
  const [text, setText] = useState<string>(initial.text);
  const [status, setStatus] = useState<EssayDraftStatusValue>(initial.status);
  const latest = useRef<string>(initial.text);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    purgeExpiredEssayDrafts(Date.now());
    const flush = (updateStatus = true) => {
      if (timer.current === undefined) {
        return;
      }
      clearTimeout(timer.current);
      timer.current = undefined;
      const saved = writeEssayDraft(storageKey, latest.current, Date.now());
      if (updateStatus) {
        setStatus(saved ? 'saved' : 'error');
      }
    };
    const onVisibility = () => {
      if (document.visibilityState === 'hidden') {
        flush();
      }
    };
    const onPageHide = () => {
      flush();
    };
    document.addEventListener('visibilitychange', onVisibility);
    window.addEventListener('pagehide', onPageHide);
    return () => {
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('pagehide', onPageHide);
      flush(false);
    };
  }, [storageKey]);

  const change = (next: string): void => {
    setText(next);
    latest.current = next;
    if (timer.current !== undefined) {
      clearTimeout(timer.current);
    }
    timer.current = setTimeout(() => {
      timer.current = undefined;
      setStatus(writeEssayDraft(storageKey, latest.current, Date.now()) ? 'saved' : 'error');
    }, essayDraftSaveDelayMilliseconds);
  };

  const discard = (): void => {
    if (timer.current !== undefined) {
      clearTimeout(timer.current);
    }
    timer.current = undefined;
    clearEssayDraft(owner);
    setStatus('idle');
  };

  return { text, change, status, discard };
}
