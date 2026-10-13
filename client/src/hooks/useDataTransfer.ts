import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { domainKeys } from './useDomains';
import { apiKeyKeys } from './useApiKeys';

export function useExportData() {
  return useMutation({
    mutationFn: async () => {
      const { blob, filename } = await api.exportData();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
      return filename;
    },
  });
}

export function useImportData() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => api.importData(file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: domainKeys.all });
      queryClient.invalidateQueries({ queryKey: apiKeyKeys.all });
    },
  });
}
