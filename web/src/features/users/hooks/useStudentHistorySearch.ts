import { getRouteApi } from '@tanstack/react-router';

const routeApi = getRouteApi('/admin/student/$studentId');

export function useStudentHistorySearch() {
  const { studentId } = routeApi.useParams();
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    studentId,
    search,
    setKind: (kind: 'Quiz' | 'Exam' | undefined) => {
      void navigate({ search: kind ? { kind, page: 1 } : { page: 1 } });
    },
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
    clearFilter: () => {
      void navigate({ search: {} });
    },
  };
}
