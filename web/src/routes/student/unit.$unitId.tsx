import { createFileRoute } from '@tanstack/react-router';
import { UnitPage } from '@/features/browse';

export const Route = createFileRoute('/student/unit/$unitId')({
  component: UnitRoute,
});

function UnitRoute() {
  const { unitId } = Route.useParams();
  return <UnitPage unitId={unitId} />;
}
