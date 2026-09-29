import type {
  AdminAvatarConversationResult,
  GetAvatarConversationsParams,
  PageDataOfAdminAvatarConversationResult,
} from '@/shared/api/generated/model';
import type { AvatarConversationSearch } from '../schemas/avatarConversationSearchSchema';

export const avatarConversationPageSize = 20;

export interface AvatarConversationPage {
  items: AdminAvatarConversationResult[];
  pageNumber: number;
  totalPages: number;
  totalItems: number;
}

function localMidnight(isoDate: string, dayOffset: number): string {
  const [year = 0, month = 1, day = 1] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day + dayOffset).toISOString();
}

export function toAvatarConversationParams(search: AvatarConversationSearch): GetAvatarConversationsParams {
  return {
    pageNumber: search.page ?? 1,
    pageSize: avatarConversationPageSize,
    ...(search.search ? { search: search.search } : {}),
    ...(search.entryPoint ? { entryPoint: search.entryPoint } : {}),
    ...(search.from ? { from: localMidnight(search.from, 0) } : {}),
    ...(search.to ? { to: localMidnight(search.to, 1) } : {}),
  };
}

export function hasActiveFilters(search: AvatarConversationSearch): boolean {
  return [search.search, search.entryPoint, search.from, search.to].some((value) => value !== undefined);
}

export function toAvatarConversationPage(data: PageDataOfAdminAvatarConversationResult): AvatarConversationPage {
  return {
    items: data.items ?? [],
    pageNumber: Number(data.pageNumber ?? 1),
    totalPages: Number(data.totalPages ?? 0),
    totalItems: Number(data.totalItems ?? 0),
  };
}
