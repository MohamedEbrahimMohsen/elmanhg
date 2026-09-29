import { keepPreviousData } from '@tanstack/react-query';
import type { TeacherInboxFilter } from '@/shared/api/generated/model';
import { useGetTeacherInbox } from '@/shared/api/generated/teacher-inbox/teacher-inbox';

export const inboxPageSize = 20;

export function useTeacherInbox(filter: TeacherInboxFilter, page: number) {
  return useGetTeacherInbox(
    { filter, pageNumber: page, pageSize: inboxPageSize },
    { query: { placeholderData: keepPreviousData } },
  );
}
