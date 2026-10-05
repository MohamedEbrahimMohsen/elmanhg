import { keepPreviousData } from '@tanstack/react-query';
import { usePreviewMultiUnitExam } from '@/shared/api/generated/exams/exams';

// PRD §7.5: a multi-unit exam covers two or more units.
export const minimumUnits = 2;

export type MultiExamBlockReason = 'inProgress' | 'chooseTwo' | 'shortfall';

export function useMultiExamBlockReason(
  subjectId: string,
  unitIds: string[],
  size: number,
  inProgress: boolean,
): MultiExamBlockReason | null {
  const enoughUnits = unitIds.length >= minimumUnits;
  const { data, isPlaceholderData } = usePreviewMultiUnitExam(
    subjectId,
    { unitIds, size },
    { query: { enabled: enoughUnits, placeholderData: keepPreviousData } },
  );

  if (inProgress) {
    return 'inProgress';
  }
  if (!enoughUnits) {
    return 'chooseTwo';
  }
  if (data && !isPlaceholderData && !data.isAvailable) {
    return 'shortfall';
  }
  return null;
}
