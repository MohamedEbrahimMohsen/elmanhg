import { recordFunnelEvent } from '@/shared/api/generated/analytics/analytics';
import type { FunnelEventType } from '@/shared/api/generated/model';

export const anonymousIdKey = 'elmanhg.anonymousId';

const onceKeyPrefix = 'elmanhg.funnel.';

export function readAnonymousId(): string {
  const stored = localStorage.getItem(anonymousIdKey);
  if (stored) {
    return stored;
  }
  const created = crypto.randomUUID();
  localStorage.setItem(anonymousIdKey, created);
  return created;
}

export interface TrackOptions {
  once?: boolean;
}

export function trackFunnelEvent(type: FunnelEventType, options: TrackOptions = {}): void {
  const onceKey = onceKeyPrefix + type;
  if (options.once) {
    if (localStorage.getItem(onceKey) !== null) {
      return;
    }
    localStorage.setItem(onceKey, '1');
  }
  // analytics must never block or break the journey
  void recordFunnelEvent({ anonymousId: readAnonymousId(), type }).catch(() => undefined);
}
