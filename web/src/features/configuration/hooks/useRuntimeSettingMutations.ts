import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetRuntimeSettingsQueryKey,
  useResetRuntimeSetting,
  useUpdateRuntimeSetting,
} from '@/shared/api/generated/configuration/configuration';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import type { SettingValue } from '../api/runtimeSettingValue';

export interface RuntimeSettingMutations {
  save: (value: SettingValue) => Promise<RuntimeSettingResult>;
  reset: () => Promise<RuntimeSettingResult>;
  isPending: boolean;
}

export function settingErrorMessage(error: unknown): string {
  return `common:errors.${error instanceof ApiError ? error.code : unhandledErrorCode}`;
}

export function useRuntimeSettingMutations(key: string): RuntimeSettingMutations {
  const { t } = useTranslation('configuration');
  const queryClient = useQueryClient();
  const onSuccess = (message: string) => () => {
    void queryClient.invalidateQueries({ queryKey: getGetRuntimeSettingsQueryKey() });
    toast.success(t(message));
  };
  const onError = () => {
    toast.error(t('toast.failed'));
  };
  const update = useUpdateRuntimeSetting({ mutation: { onSuccess: onSuccess('toast.saved'), onError } });
  const reset = useResetRuntimeSetting({ mutation: { onSuccess: onSuccess('toast.reset'), onError } });

  return {
    save: (value) => update.mutateAsync({ key, data: { value } }),
    reset: () => reset.mutateAsync({ key }),
    isPending: update.isPending || reset.isPending,
  };
}
