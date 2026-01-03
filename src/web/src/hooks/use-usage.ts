'use client';

import { useQuery } from '@tanstack/react-query';
import { usageApi } from '@/lib/api';

export function useUsageSummary() {
  return useQuery({
    queryKey: ['usage', 'summary'],
    queryFn: async () => {
      const response = await usageApi.getSummary();
      return response.data;
    },
  });
}

// Alias for convenience
export const useUsage = useUsageSummary;

export function useUsageHistory(months?: number) {
  return useQuery({
    queryKey: ['usage', 'history', months],
    queryFn: async () => {
      const response = await usageApi.getHistory(months);
      return response.data;
    },
  });
}
