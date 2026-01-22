import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { useDomains, useCreateDomain, useDeleteDomain } from '@/hooks/useDomains';
import { DomainCard } from '@/components/DomainCard';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

function DomainCardSkeleton() {
  return (
    <Card className="overflow-hidden border-border/70">
      <div className="h-1 bg-primary" />
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-5 w-20 rounded-full" />
      </CardHeader>
      <CardContent>
        <Skeleton className="mb-4 h-4 w-48" />
        <Skeleton className="h-10 w-24 rounded-full" />
      </CardContent>
    </Card>
  );
}

export function DomainsPage() {
  const [newDomain, setNewDomain] = useState('');
  const [page, setPage] = useState(1);
  const [showAddModal, setShowAddModal] = useState(false);
  const [showErrorModal, setShowErrorModal] = useState(false);
  const limit = 10;

  const { data, isLoading, error } = useDomains(page, limit);
  const createMutation = useCreateDomain();
  const deleteMutation = useDeleteDomain();
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
      await createMutation.mutateAsync(newDomain.trim());
      toast.success(`Domain ${newDomain.trim()} added`);
      setNewDomain('');
      return true;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to add domain');
      return false;
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
    <div className="container mx-auto px-4 py-10 md:py-16">
      {!hasDomains && (
        <section className="mx-auto grid max-w-xl gap-6">
          <div className="space-y-3">
            <h2 className="font-display text-3xl font-semibold leading-tight md:text-4xl">
              Add your first domain
            </h2>
            <p className="text-muted-foreground">
              Enter your domain below to get started. We'll set up your domain in AWS SES and
              provide you with the exact DNS records you need to add. Once configured, we'll
              verify your email setup and continuously monitor your domain's health, including
              MX, SPF, DKIM, and DMARC records.
            </p>
          </div>
          <Card className="border-border/60">
            <CardContent className="pt-6">
              <form onSubmit={handleCreate} className="grid gap-4">
                <Input
                  placeholder="example.com"
                  value={newDomain}
                  onChange={(e) => setNewDomain(e.target.value)}
                  className="h-12 text-base"
                />
                <Button
                  type="submit"
                  size="lg"
                  disabled={createMutation.isPending || !newDomain.trim()}
                >
                  {createMutation.isPending ? 'Adding...' : 'Add domain'}
                </Button>
              </form>
            </CardContent>
          </Card>
        </section>
      )}

      {isLoading && (
        <div className="mt-10 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {[...Array(3)].map((_, i) => (
            <DomainCardSkeleton key={i} />
          ))}
        </div>
      )}

      {error && !showInitialError && (
        <div className="mt-6 rounded-2xl border border-[var(--status-failed-text)]/40 bg-[var(--status-failed-bg)]/70 px-4 py-3 text-sm text-[var(--status-failed-text)]">
          <div className="text-xs uppercase tracking-[0.2em]">Error</div>
          <div className="mt-1 font-semibold">Unable to load domains</div>
          <div className="mt-1 text-sm text-[var(--status-failed-text)]/90">
            {error.message}
          </div>
        </div>
      )}

      {data && hasDomains && (
        <>
          <div className="mt-4 flex flex-wrap items-center justify-between gap-4">
            <div>
              <h3 className="font-display text-2xl">All domains</h3>
            </div>
            <div className="flex items-center gap-3">
              <span className="text-xs uppercase tracking-[0.2em] text-muted-foreground">
                {totalDomains} total
              </span>
              <Button onClick={() => setShowAddModal(true)} size="sm">
                Add domain
              </Button>
            </div>
          </div>

          <div className="mt-6 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
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
            <div className="mt-10 rounded-3xl border border-dashed border-border/70 bg-background/60 p-10 text-center">
              <p className="font-display text-xl">No domains yet.</p>
              <p className="mt-2 text-sm text-muted-foreground">
                Add your first domain above to start verification and health tracking.
              </p>
            </div>
          )}

          {totalPages > 1 && (
            <div className="mt-10 flex flex-wrap justify-center gap-3">
              <Button
                variant="outline"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                Previous
              </Button>
              <span className="flex items-center rounded-full border border-border/70 bg-background/70 px-4 text-sm">
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

      {(showAddModal || (showInitialError && showErrorModal)) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-8">
          <div
            className="absolute inset-0 bg-black/40"
            onClick={() => {
              if (showAddModal) setShowAddModal(false);
              if (showErrorModal) setShowErrorModal(false);
            }}
          />
          {showAddModal && (
            <Card
              className="relative z-10 w-full max-w-lg border-border/70"
              role="dialog"
              aria-modal="true"
              aria-label="Add domain"
            >
              <CardHeader className="flex flex-row items-center justify-between space-y-0">
                <CardTitle className="text-xl">Add domain</CardTitle>
                <Button variant="ghost" size="sm" onClick={() => setShowAddModal(false)}>
                  Close
                </Button>
              </CardHeader>
              <CardContent>
                <form
                  onSubmit={async (e) => {
                    const created = await handleCreate(e);
                    if (created) setShowAddModal(false);
                  }}
                  className="grid gap-4"
                >
                  <Input
                    placeholder="example.com"
                    value={newDomain}
                    onChange={(e) => setNewDomain(e.target.value)}
                    className="h-12 text-base"
                  />
                  <Button
                    type="submit"
                    size="lg"
                    disabled={createMutation.isPending || !newDomain.trim()}
                  >
                    {createMutation.isPending ? 'Adding...' : 'Add domain'}
                  </Button>
                </form>
              </CardContent>
            </Card>
          )}

          {showInitialError && showErrorModal && (
            <Card
              className="relative z-10 w-full max-w-lg border-[var(--status-failed-text)]/40"
              role="alertdialog"
              aria-modal="true"
              aria-label="Error loading domains"
            >
              <CardHeader className="flex flex-row items-center justify-between space-y-0">
                <CardTitle className="text-xl text-[var(--status-failed-text)]">
                  Unable to load domains
                </CardTitle>
                <Button variant="ghost" size="sm" onClick={() => setShowErrorModal(false)}>
                  Close
                </Button>
              </CardHeader>
              <CardContent>
                <div className="rounded-2xl border border-[var(--status-failed-text)]/30 bg-[var(--status-failed-bg)]/70 px-4 py-3 text-sm text-[var(--status-failed-text)]">
                  {error.message}
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}
