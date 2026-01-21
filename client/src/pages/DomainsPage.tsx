import { useState } from 'react';
import { useDomains, useCreateDomain, useDeleteDomain } from '@/hooks/useDomains';
import { DomainCard } from '@/components/DomainCard';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export function DomainsPage() {
  const [newDomain, setNewDomain] = useState('');
  const [page, setPage] = useState(1);
  const limit = 10;

  const { data, isLoading, error } = useDomains(page, limit);
  const createMutation = useCreateDomain();
  const deleteMutation = useDeleteDomain();

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newDomain.trim()) return;

    try {
      await createMutation.mutateAsync(newDomain.trim());
      setNewDomain('');
    } catch {
      // Error is handled by mutation
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteMutation.mutateAsync(id);
    } catch {
      // Error is handled by mutation
    }
  };

  const totalPages = data ? Math.ceil(data.total / limit) : 0;

  return (
    <div className="container mx-auto py-8 px-4">
      <h1 className="text-3xl font-bold mb-8">Domains</h1>

      <Card className="mb-8">
        <CardHeader>
          <CardTitle>Add Domain</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleCreate} className="flex gap-4">
            <Input
              placeholder="example.com"
              value={newDomain}
              onChange={(e) => setNewDomain(e.target.value)}
              className="flex-1"
            />
            <Button type="submit" disabled={createMutation.isPending}>
              {createMutation.isPending ? 'Adding...' : 'Add Domain'}
            </Button>
          </form>
          {createMutation.error && (
            <p className="text-red-600 mt-2 text-sm">
              {createMutation.error.message}
            </p>
          )}
        </CardContent>
      </Card>

      {isLoading && <p>Loading domains...</p>}

      {error && <p className="text-red-600">Error loading domains: {error.message}</p>}

      {data && (
        <>
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {data.data.map((domain) => (
              <DomainCard
                key={domain.id}
                domain={domain}
                onDelete={handleDelete}
                isDeleting={deleteMutation.isPending}
              />
            ))}
          </div>

          {data.data.length === 0 && (
            <p className="text-muted-foreground text-center py-8">
              No domains yet. Add your first domain above.
            </p>
          )}

          {totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-8">
              <Button
                variant="outline"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                Previous
              </Button>
              <span className="flex items-center px-4">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
              >
                Next
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
