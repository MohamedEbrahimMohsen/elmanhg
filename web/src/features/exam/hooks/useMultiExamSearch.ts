import { getRouteApi } from '@tanstack/react-router';

const routeApi = getRouteApi('/student/multi-exam');

export interface MultiExamSelection {
  subjectId: string | undefined;
  unitIds: string[];
  size: number | undefined;
  selectSubject: (id: string) => void;
  toggleUnit: (id: string, checked: boolean) => void;
  selectSize: (size: number) => void;
}

export function useMultiExamSearch(): MultiExamSelection {
  const { subjectId, unitIds = [], size } = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    subjectId,
    unitIds,
    size,
    selectSubject: (id) => {
      void navigate({ search: { subjectId: id } });
    },
    toggleUnit: (id, checked) => {
      void navigate({
        search: (prev) => {
          const others = (prev.unitIds ?? []).filter((unitId) => unitId !== id);
          return { ...prev, unitIds: checked ? [...others, id] : others };
        },
        replace: true,
      });
    },
    selectSize: (selected) => {
      void navigate({ search: (prev) => ({ ...prev, size: selected }), replace: true });
    },
  };
}
