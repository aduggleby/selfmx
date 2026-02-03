import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { CursorPagedSentEmails } from '@/lib/schemas';

export interface SentEmailFilters {
  domainId?: string;
  from?: string;
  to?: string;
  pageSize?: number;
}

export const sentEmailKeys = {
  all: ['sentEmails'] as const,
  lists: () => [...sentEmailKeys.all, 'list'] as const,
  list: (filters: SentEmailFilters) => [...sentEmailKeys.lists(), filters] as const,
  details: () => [...sentEmailKeys.all, 'detail'] as const,
  detail: (id: string) => [...sentEmailKeys.details(), id] as const,
};

export function useSentEmails(filters: SentEmailFilters = {}) {
  return useInfiniteQuery({
    queryKey: sentEmailKeys.list(filters),
    queryFn: ({ pageParam }) =>
      api.listSentEmails({ ...filters, cursor: pageParam }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage: CursorPagedSentEmails) =>
      lastPage.hasMore ? lastPage.nextCursor : undefined,
    staleTime: 60 * 1000,
  });
}

export function useSentEmail(id: string | null) {
  return useQuery({
    queryKey: sentEmailKeys.detail(id ?? ''),
    queryFn: () => api.getSentEmail(id!),
    enabled: !!id,
    staleTime: 5 * 60 * 1000,
  });
}
