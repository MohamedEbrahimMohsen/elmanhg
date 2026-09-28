import { useEffect, useRef, useState } from 'react';
import { remainingMilliseconds } from '../api/examSession';

const tickMilliseconds = 1000;

export function useExamCountdown(
  deadline: string | null,
  serverNow: string,
  receivedAt: number,
  onExpire: () => void,
): number | null {
  const [remaining, setRemaining] = useState<number | null>(() =>
    deadline === null ? null : remainingMilliseconds(deadline, Date.parse(serverNow) - receivedAt, Date.now()),
  );
  const expired = useRef(false);
  const onExpireRef = useRef(onExpire);

  useEffect(() => {
    onExpireRef.current = onExpire;
  });

  useEffect(() => {
    if (deadline === null) {
      return;
    }
    const offset = Date.parse(serverNow) - receivedAt;
    const tick = () => {
      const left = remainingMilliseconds(deadline, offset, Date.now());
      setRemaining(left);
      if (left === 0) {
        clearInterval(interval);
        if (!expired.current) {
          expired.current = true;
          onExpireRef.current();
        }
      }
    };
    const interval = setInterval(tick, tickMilliseconds);
    tick();
    return () => {
      clearInterval(interval);
    };
  }, [deadline, serverNow, receivedAt]);

  return deadline === null ? null : remaining;
}
