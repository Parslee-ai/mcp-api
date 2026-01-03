'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apisApi } from '@/lib/api';

export function useApis() {
  return useQuery({
    queryKey: ['apis'],
    queryFn: async () => {
      const response = await apisApi.getAll();
      return response.data;
    },
  });
}

export function useApi(id: string) {
  return useQuery({
    queryKey: ['apis', id],
    queryFn: async () => {
      const response = await apisApi.get(id);
      return response.data;
    },
    enabled: !!id,
  });
}

export interface RegisterApiRequest {
  name: string;
  specUrl: string;
  description?: string;
  authType: string;
  apiKey?: string;
  apiKeyHeader?: string;
  bearerToken?: string;
  basicUsername?: string;
  basicPassword?: string;
}

export function useRegisterApi() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: RegisterApiRequest) => {
      const response = await apisApi.register(data);
      return response.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['apis'] });
    },
  });
}

export function useUpdateApi() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      id,
      data,
    }: {
      id: string;
      data: { displayName?: string; isEnabled?: boolean };
    }) => {
      const response = await apisApi.update(id, data);
      return response.data;
    },
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: ['apis'] });
      queryClient.invalidateQueries({ queryKey: ['apis', id] });
    },
  });
}

export function useDeleteApi() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      await apisApi.delete(id);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['apis'] });
    },
  });
}

export function useToggleApi() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, enabled }: { id: string; enabled: boolean }) => {
      const response = await apisApi.toggle(id, enabled);
      return response.data;
    },
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: ['apis'] });
      queryClient.invalidateQueries({ queryKey: ['apis', id] });
    },
  });
}

export function useRefreshApi() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      const response = await apisApi.refresh(id);
      return response.data;
    },
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: ['apis'] });
      queryClient.invalidateQueries({ queryKey: ['apis', id] });
    },
  });
}

export function useApiEndpoints(apiId: string) {
  return useQuery({
    queryKey: ['apis', apiId, 'endpoints'],
    queryFn: async () => {
      const response = await apisApi.getEndpoints(apiId);
      return response.data;
    },
    enabled: !!apiId,
  });
}

export function useToggleEndpoint() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      apiId,
      endpointId,
      enabled,
    }: {
      apiId: string;
      endpointId: string;
      enabled: boolean;
    }) => {
      const response = await apisApi.toggleEndpoint(apiId, endpointId, enabled);
      return response.data;
    },
    onSuccess: (_, { apiId }) => {
      queryClient.invalidateQueries({ queryKey: ['apis', apiId] });
      queryClient.invalidateQueries({ queryKey: ['apis', apiId, 'endpoints'] });
    },
  });
}
