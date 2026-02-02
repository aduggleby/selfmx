import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { useDomains, useCreateDomain } from '@/hooks/useDomains';
import { DomainCard } from '@/components/DomainCard';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

function DomainCardSkeleton() {
  return (
    <Card>
      <div className="p-5 pb-3">
        <div className="flex items-center justify-between gap-3">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-5 w-16 rounded" />
        </div>
        <Skeleton className="mt-2 h-3 w-40" />
      </div>
      <div className="px-5 pb-5">
        <Skeleton className="h-7 w-16" />
      </div>
    </Card>
  );
}

export function DomainsPage() {
  const navigate = useNavigate();
  const [newDomain, setNewDomain] = useState('');
  const [page, setPage] = useState(1);
  const [showAddModal, setShowAddModal] = useState(false);
  const [showErrorModal, setShowErrorModal] = useState(false);
  const limit = 10;

  const { data, isLoading, error } = useDomains(page, limit);
  const createMutation = useCreateDomain();
  const domains = data?.data ?? [];
  const totalDomains = data?.total ?? 0;
  const hasDomains = domains.length > 0;
  const showInitialError = !!error && !data && !isLoading;

  useEffect(() => {
    if (showInitialError) {
      setShowErrorModal(true);
    }
  }, [showInitialError]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newDomain.trim()) return;

    try {
      const created = await createMutation.mutateAsync(newDomain.trim());
      toast.success(`Domain ${newDomain.trim()} added`);
      setNewDomain('');
      navigate(`/domains/${created.id}`);
      return true;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to add domain');
      return false;
    }
  };

  const totalPages = data ? Math.ceil(data.total / limit) : 0;

  return (
    <div className="container mx-auto max-w-4xl px-4 py-8">
      {!hasDomains && !isLoading && (
        <section className="mx-auto max-w-md">
          <div className="mb-6">
            <h2 className="font-display text-xl font-semibold">
              Add your first domain
            </h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Enter a domain to set up AWS SES verification. We'll provide DNS records for MX, SPF, DKIM, and DMARC.
            </p>
          </div>
          <form onSubmit={handleCreate} className="flex gap-2">
            <Input
              placeholder="example.com"
              value={newDomain}
              onChange={(e) => setNewDomain(e.target.value)}
              className="font-mono"
            />
            <Button
              type="submit"
              disabled={createMutation.isPending || !newDomain.trim()}
            >
              {createMutation.isPending ? 'Adding...' : 'Add'}
            </Button>
          </form>
        </section>
      )}

      {isLoading && (
        <div className="grid gap-4 sm:grid-cols-2">
          {[...Array(4)].map((_, i) => (
            <DomainCardSkeleton key={i} />
          ))}
        </div>
      )}

      {error && !showInitialError && (
        <div className="rounded border border-[var(--status-failed-text)]/20 bg-[var(--status-failed-bg)] px-4 py-3 text-sm text-[var(--status-failed-text)]">
          <span className="font-medium">Error:</span> {error.message}
        </div>
      )}

      {data && hasDomains && (
        <>
          <div className="mb-4 flex items-center justify-between">
            <div className="text-sm text-muted-foreground">
              {totalDomains} domain{totalDomains !== 1 ? 's' : ''}
            </div>
            <Button onClick={() => setShowAddModal(true)} size="sm">
              Add domain
            </Button>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            {data.data.map((domain) => (
              <DomainCard
                key={domain.id}
                domain={domain}
              />
            ))}
          </div>

          {totalPages > 1 && (
            <div className="mt-6 flex items-center justify-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                Previous
              </Button>
              <span className="px-2 text-sm text-muted-foreground">
                {page} / {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
              >
                Next
              </Button>
            </div>
          )}
        </>
      )}

      {(showAddModal || (showInitialError && showErrorModal)) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div
            className="absolute inset-0 bg-black/50"
            onClick={() => {
              if (showAddModal) setShowAddModal(false);
              if (showErrorModal) setShowErrorModal(false);
            }}
          />
          {showAddModal && (
            <Card className="relative z-10 w-full max-w-sm" role="dialog" aria-modal="true">
              <div className="p-5">
                <div className="mb-4 flex items-center justify-between">
                  <h3 className="font-display text-lg font-semibold">Add domain</h3>
                  <Button variant="ghost" size="sm" onClick={() => setShowAddModal(false)}>
                    Close
                  </Button>
                </div>
                <form
                  onSubmit={async (e) => {
                    const created = await handleCreate(e);
                    if (created) setShowAddModal(false);
                  }}
                  className="flex gap-2"
                >
                  <Input
                    placeholder="example.com"
                    value={newDomain}
                    onChange={(e) => setNewDomain(e.target.value)}
                    className="font-mono"
                    autoFocus
                  />
                  <Button
                    type="submit"
                    disabled={createMutation.isPending || !newDomain.trim()}
                  >
                    {createMutation.isPending ? 'Adding...' : 'Add'}
                  </Button>
                </form>
              </div>
            </Card>
          )}

          {showInitialError && showErrorModal && (
            <Card className="relative z-10 w-full max-w-sm" role="alertdialog" aria-modal="true">
              <div className="p-5">
                <div className="mb-4 flex items-center justify-between">
                  <h3 className="font-display text-lg font-semibold text-destructive">
                    Error
                  </h3>
                  <Button variant="ghost" size="sm" onClick={() => setShowErrorModal(false)}>
                    Close
                  </Button>
                </div>
                <p className="text-sm text-muted-foreground">{error.message}</p>
              </div>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}
