import { useGetInfrastructureConfiguration as useGetInfrastructureConfigurationQuery } from '@/shared/api/generated/configuration/configuration';

export function useGetInfrastructureConfiguration() {
  return useGetInfrastructureConfigurationQuery();
}
