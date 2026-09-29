import type {
  BillingPeriod,
  EntitlementResult,
  PlanCatalogueResult,
  SubscriptionPlan,
} from '@/shared/api/generated/model';
import { usePlanCardContent } from '../hooks/usePlanCardContent';
import { AskTeacherCheckoutAction } from './AskTeacherCheckoutAction';
import { BaseCheckoutActions } from './BaseCheckoutActions';
import { PlanCard } from './PlanCard';

export interface PlanCardGridProps {
  catalogue: PlanCatalogueResult;
  entitlement: EntitlementResult;
  onCheckout: (plan: SubscriptionPlan, period: BillingPeriod) => void;
  isCheckoutPending: boolean;
}

type CheckoutMode = 'subscribe' | 'renew' | null;

export function PlanCardGrid({ catalogue, entitlement, onCheckout, isCheckoutPending }: PlanCardGridProps) {
  const content = usePlanCardContent(catalogue);
  const held = (plan: SubscriptionPlan) => entitlement.subscriptions.find((s) => s.plan === plan);
  const modeFor = (plan: SubscriptionPlan, holds: boolean): CheckoutMode => {
    if (!holds) {
      return 'subscribe';
    }
    return held(plan)?.canRenew ? 'renew' : null;
  };
  const baseMode = modeFor('Base', entitlement.tier === 'Base');
  const askTeacherMode = modeFor('AskTeacher', entitlement.hasAskTeacher);

  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
      <PlanCard {...content.free} isActive={false} />
      <PlanCard
        {...content.base}
        isActive={entitlement.tier === 'Base'}
        actions={
          baseMode === null ? null : (
            <BaseCheckoutActions
              mode={baseMode}
              prices={catalogue.base.prices}
              disabled={isCheckoutPending}
              onCheckout={(period) => {
                onCheckout('Base', period);
              }}
            />
          )
        }
      />
      <PlanCard
        {...content.askTeacher}
        isActive={entitlement.hasAskTeacher}
        actions={
          askTeacherMode === null ? null : (
            <AskTeacherCheckoutAction
              mode={askTeacherMode}
              hasBase={entitlement.tier === 'Base'}
              disabled={isCheckoutPending}
              onCheckout={() => {
                onCheckout('AskTeacher', 'Monthly');
              }}
            />
          )
        }
      />
    </div>
  );
}
