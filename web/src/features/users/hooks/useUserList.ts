import { keepPreviousData } from '@tanstack/react-query';
import { useGetUsers } from '@/shared/api/generated/users/users';
import { toGetUsersParams, toUserListPage } from '../api/userListParams';
import type { UsersSearch } from '../schemas/usersSearchSchema';

export function useUserList(search: UsersSearch) {
  return useGetUsers(toGetUsersParams(search), {
    query: { placeholderData: keepPreviousData, select: toUserListPage },
  });
}
