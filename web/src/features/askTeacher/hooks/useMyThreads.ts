import { keepPreviousData } from '@tanstack/react-query';
import { useGetMyTeacherThreads } from '@/shared/api/generated/teacher-threads/teacher-threads';

export const threadListPageSize = 20;

export function useMyThreads(page: number) {
  return useGetMyTeacherThreads(
    { pageNumber: page, pageSize: threadListPageSize },
    { query: { placeholderData: keepPreviousData } },
  );
}
