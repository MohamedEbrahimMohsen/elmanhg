import { getRouteApi } from '@tanstack/react-router';

const routeApi = getRouteApi('/admin/blueprints');

export function useBlueprintsSearch(): { subjectId: string | undefined; selectSubject: (subjectId: string) => void } {
  const { subjectId } = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    subjectId,
    selectSubject: (selected) => {
      void navigate({ search: { subjectId: selected } });
    },
  };
}
