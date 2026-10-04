import { useQueries } from '@tanstack/react-query';
import { getGetStudentUnitQueryOptions, useGetStudentSubject } from '@/shared/api/generated/browse/browse';

export interface AssistantLessonOption {
  id: string;
  name: string;
  isLocked: boolean;
}

export interface AssistantLessonGroup {
  unitId: string;
  unitName: string;
  lessons: AssistantLessonOption[];
}

export function useAssistantLessonOptions(subjectId: string): {
  groups: AssistantLessonGroup[];
  isPending: boolean;
  isError: boolean;
  retry: () => void;
} {
  const subject = useGetStudentSubject(subjectId, { query: { enabled: subjectId !== '' } });
  const units = (subject.data?.units ?? []).filter((unit) => Number(unit.lessonCount) > 0);
  const results = useQueries({ queries: units.map((unit) => getGetStudentUnitQueryOptions(unit.id)) });

  const groups = units.flatMap((unit, index): AssistantLessonGroup[] => {
    const data = results[index]?.data;
    return data === undefined
      ? []
      : [
          {
            unitId: unit.id,
            unitName: unit.name,
            lessons: data.lessons.map(({ id, name, isLocked }) => ({ id, name, isLocked })),
          },
        ];
  });

  return {
    groups,
    isPending: subjectId !== '' && (subject.isPending || results.some((result) => result.isPending)),
    isError: subject.isError || results.some((result) => result.isError),
    retry: () => {
      if (subject.isError) void subject.refetch();
      for (const result of results) {
        if (result.isError) void result.refetch();
      }
    },
  };
}
