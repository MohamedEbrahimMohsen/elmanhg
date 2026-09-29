import type { PlanCatalogueResult } from '@/shared/api/generated/model';
import { usePlanCardContent } from '../hooks/usePlanCardContent';
import { PlanCard } from './PlanCard';

export interface PublicPlanCardsProps {
  catalogue: PlanCatalogueResult;
}

export function PublicPlanCards({ catalogue }: PublicPlanCardsProps) {
  const content = usePlanCardContent(catalogue);

  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
      <PlanCard {...content.free} isActive={false} />
      <PlanCard {...content.base} isActive={false} />
      <PlanCard {...content.askTeacher} isActive={false} />
    </div>
  );
}
