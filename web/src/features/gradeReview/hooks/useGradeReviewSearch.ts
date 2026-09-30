import { getRouteApi } from '@tanstack/react-router';
import type { GradeReviewKindValue } from '../api/gradeReviewOptions';

const routeApi = getRouteApi('/teacher/grades');

export function useGradeReviewSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    selectSubject: (subjectId: string) => {
      void navigate({ search: { subjectId, kind: search.kind } });
    },
    selectKind: (kind: GradeReviewKindValue) => {
      void navigate({ search: (previous) => ({ ...previous, kind, page: undefined }) });
    },
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
  };
}
