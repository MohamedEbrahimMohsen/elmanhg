import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { useRecordLessonOpening } from '@/shared/api/generated/browse/browse';
import { getGetUnitExamOverviewQueryKey } from '@/shared/api/generated/exams/exams';

export function useLessonOpening(lessonId: string, unitId: string | undefined): void {
  const queryClient = useQueryClient();
  const recorded = useRef<string | null>(null);
  const { mutate } = useRecordLessonOpening({
    mutation: {
      onSuccess: () => {
        if (unitId) {
          void queryClient.invalidateQueries({ queryKey: getGetUnitExamOverviewQueryKey(unitId) });
        }
      },
    },
  });

  useEffect(() => {
    if (unitId === undefined || recorded.current === lessonId) {
      return;
    }
    recorded.current = lessonId;
    mutate({ lessonId });
  }, [lessonId, unitId, mutate]);
}
