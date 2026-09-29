import type { EntitlementResult } from '@/shared/api/generated/model';

export type PlanLabelKey = 'free' | 'base' | 'baseWithAskTeacher';

export function planLabelKey(entitlement: Pick<EntitlementResult, 'tier' | 'hasAskTeacher'>): PlanLabelKey {
  if (entitlement.tier === 'Free') {
    return 'free';
  }
  return entitlement.hasAskTeacher ? 'baseWithAskTeacher' : 'base';
}
