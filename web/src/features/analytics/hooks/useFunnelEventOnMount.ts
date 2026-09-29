import { useEffect } from 'react';
import type { FunnelEventType } from '@/shared/api/generated/model';
import { trackFunnelEvent } from '../api/funnelTracker';

export function useFunnelEventOnMount(type: FunnelEventType): void {
  useEffect(() => {
    trackFunnelEvent(type);
  }, [type]);
}
