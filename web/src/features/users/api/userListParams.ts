import type {
  GetUsersParams,
  PageDataOfUserSummaryResult,
  UserRole,
  UserSummaryResult,
} from '@/shared/api/generated/model';
import type { UserListTab, UsersSearch } from '../schemas/usersSearchSchema';

export const userListPageSize = 20;

export const tabRole: Record<UserListTab, UserRole> = { students: 'Student', teachers: 'Teacher', admins: 'Admin' };

export interface UserListPage {
  items: UserSummaryResult[];
  pageNumber: number;
  totalPages: number;
}

export function toGetUsersParams(search: UsersSearch): GetUsersParams {
  return {
    role: tabRole[search.tab ?? 'students'],
    pageNumber: search.page ?? 1,
    pageSize: userListPageSize,
    ...(search.q ? { search: search.q } : {}),
    ...(search.status ? { status: search.status } : {}),
  };
}

export function hasActiveFilters(search: UsersSearch): boolean {
  return search.q !== undefined || search.status !== undefined;
}

export function toUserListPage(data: PageDataOfUserSummaryResult): UserListPage {
  return {
    items: data.items ?? [],
    pageNumber: Number(data.pageNumber ?? 1),
    totalPages: Number(data.totalPages ?? 0),
  };
}
