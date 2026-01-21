import { useState } from 'react';
import { toast } from 'sonner';
import { useDomains, useCreateDomain, useDeleteDomain } from '@/hooks/useDomains';
import { DomainCard } from '@/components/DomainCard';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

function DomainCardSkeleton() {
  return (
    <Card className="shadow-sm">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-5 w-16 rounded-full" />
      </CardHeader>
      <CardContent>
        <Skeleton className="h-4 w-48 mb-4" />
        <Skeleton className="h-9 w-20" />
      </CardContent>
    </Card>
  );
}

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
      toast.success(`Domain ${newDomain.trim()} added`);
      setNewDomain('');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to add domain');
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteMutation.mutateAsync(id);
      toast.success('Domain deleted');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to delete domain');
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
        </CardContent>
      </Card>

      {isLoading && (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {[...Array(3)].map((_, i) => (
            <DomainCardSkeleton key={i} />
          ))}
        </div>
      )}

      {error && (
        <p className="text-[var(--status-failed-text)]">
          Error loading domains: {error.message}
        </p>
      )}

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
