import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { PaginatedApiKeys } from '@/lib/schemas';

export const apiKeyKeys = {
  all: ['apiKeys'] as const,
  lists: () => [...apiKeyKeys.all, 'list'] as const,
  list: (page: number, limit: number) => [...apiKeyKeys.lists(), { page, limit }] as const,
  details: () => [...apiKeyKeys.all, 'detail'] as const,
  detail: (id: string) => [...apiKeyKeys.details(), id] as const,
};

export function useApiKeys(page = 1, limit = 20) {
  return useQuery({
    queryKey: apiKeyKeys.list(page, limit),
    queryFn: () => api.listApiKeys(page, limit),
  });
}

export function useCreateApiKey() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (params: { name: string; domainIds?: string[]; isAdmin?: boolean }) =>
      api.createApiKey(params),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: apiKeyKeys.lists() });
    },
  });
}

export function useDeleteApiKey() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.deleteApiKey(id),
    onMutate: async (deletedId: string) => {
      await queryClient.cancelQueries({ queryKey: apiKeyKeys.lists() });

      const previousData = queryClient.getQueriesData<PaginatedApiKeys>({
        queryKey: apiKeyKeys.lists(),
      });

      queryClient.setQueriesData<PaginatedApiKeys>(
        { queryKey: apiKeyKeys.lists() },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            data: old.data.filter((k) => k.id !== deletedId),
            total: old.total - 1,
          };
        }
      );

      return { previousData };
    },
    onError: (_err, _id, context) => {
      if (context?.previousData) {
        for (const [queryKey, data] of context.previousData) {
          queryClient.setQueryData(queryKey, data);
        }
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: apiKeyKeys.lists() });
    },
  });
}
