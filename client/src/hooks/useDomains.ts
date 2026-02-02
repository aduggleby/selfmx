import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { Domain, PaginatedDomains, SendEmailResponse } from '@/lib/schemas';

export const domainKeys = {
  all: ['domains'] as const,
  lists: () => [...domainKeys.all, 'list'] as const,
  list: (page: number, limit: number) => [...domainKeys.lists(), { page, limit }] as const,
  details: () => [...domainKeys.all, 'detail'] as const,
  detail: (id: string) => [...domainKeys.details(), id] as const,
};

export function useDomains(page = 1, limit = 20) {
  return useQuery({
    queryKey: domainKeys.list(page, limit),
    queryFn: () => api.listDomains(page, limit),
  });
}

export function useDomain(id: string) {
  return useQuery({
    queryKey: domainKeys.detail(id),
    queryFn: () => api.getDomain(id),
    enabled: !!id,
    // Poll every 5 seconds when domain is pending/verifying, stop when verified/failed
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status === 'pending' || status === 'verifying') {
        return 5000; // 5 seconds
      }
      return false; // Stop polling
    },
  });
}

export function useCreateDomain() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (name: string) => api.createDomain(name),
    onSuccess: (newDomain: Domain) => {
      queryClient.invalidateQueries({ queryKey: domainKeys.lists() });
      queryClient.setQueryData<Domain>(domainKeys.detail(newDomain.id), newDomain);
    },
  });
}

export function useDeleteDomain() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.deleteDomain(id),
    onMutate: async (deletedId: string) => {
      await queryClient.cancelQueries({ queryKey: domainKeys.lists() });

      const previousData = queryClient.getQueriesData<PaginatedDomains>({
        queryKey: domainKeys.lists(),
      });

      queryClient.setQueriesData<PaginatedDomains>(
        { queryKey: domainKeys.lists() },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            data: old.data.filter((d) => d.id !== deletedId),
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
      queryClient.invalidateQueries({ queryKey: domainKeys.lists() });
    },
  });
}

export function useSendTestEmail() {
  return useMutation({
    mutationFn: ({
      domainId,
      senderPrefix,
      to,
      subject,
      text,
    }: {
      domainId: string;
      senderPrefix: string;
      to: string;
      subject: string;
      text: string;
    }): Promise<SendEmailResponse> =>
      api.sendTestEmail(domainId, { senderPrefix, to, subject, text }),
  });
}
