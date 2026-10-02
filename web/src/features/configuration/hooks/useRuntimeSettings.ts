import { useGetRuntimeSettings as useGetRuntimeSettingsQuery } from '@/shared/api/generated/configuration/configuration';

export function useGetRuntimeSettings() {
  return useGetRuntimeSettingsQuery();
}
