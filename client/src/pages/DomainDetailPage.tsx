import { useParams, useNavigate, Link } from 'react-router-dom';
import { toast } from 'sonner';
import { ArrowLeft, Trash2 } from 'lucide-react';
import { useDomain, useDeleteDomain } from '@/hooks/useDomains';
import { DomainStatusBadge } from '@/components/DomainStatusBadge';
import { DnsRecordsTable } from '@/components/DnsRecordsTable';
import { DnsActions } from '@/components/DnsActions';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

function DomainDetailSkeleton() {
  return (
    <div className="container mx-auto px-4 py-10 md:py-16">
      <Skeleton className="h-5 w-32 mb-8" />
      <Card className="border-border/70">
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <Skeleton className="h-7 w-48" />
            <Skeleton className="h-6 w-24 rounded-full" />
          </div>
        </CardHeader>
        <CardContent className="space-y-6">
          <Skeleton className="h-4 w-64" />
          <Skeleton className="h-32 w-full" />
        </CardContent>
      </Card>
    </div>
  );
}

export function DomainDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: domain, isLoading, error } = useDomain(id ?? '');
  const deleteMutation = useDeleteDomain();

  const handleDelete = async () => {
    if (!domain) return;

    try {
      await deleteMutation.mutateAsync(domain.id);
      toast.success('Domain deleted');
      navigate('/');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to delete domain');
    }
  };

  if (isLoading) {
    return <DomainDetailSkeleton />;
  }

  if (error || !domain) {
    return (
      <div className="container mx-auto px-4 py-10 md:py-16">
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors mb-8"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to domains
        </Link>
        <Card className="border-[var(--status-failed-text)]/40">
          <CardHeader>
            <CardTitle className="text-[var(--status-failed-text)]">Domain not found</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-muted-foreground">
              {error?.message ?? 'The requested domain could not be found.'}
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  const hasDnsRecords = domain.dnsRecords && domain.dnsRecords.length > 0;

  return (
    <div className="container mx-auto px-4 py-10 md:py-16">
      <Link
        to="/"
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors mb-8"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to domains
      </Link>

      <Card className="border-border/70 overflow-hidden">
        <div className="h-1 bg-primary" />
        <CardHeader>
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3">
              <CardTitle className="text-2xl">{domain.name}</CardTitle>
              <DomainStatusBadge status={domain.status} />
            </div>
            <Button
              variant="destructive"
              size="sm"
              onClick={handleDelete}
              disabled={deleteMutation.isPending}
              className="rounded-full w-fit"
            >
              <Trash2 className="h-4 w-4 mr-2" />
              {deleteMutation.isPending ? 'Deleting...' : 'Delete domain'}
            </Button>
          </div>
          <div className="flex flex-wrap gap-3 text-xs text-muted-foreground mt-2">
            <span className="rounded-full border border-border/70 bg-background/70 px-2.5 py-1">
              Added {new Date(domain.createdAt).toLocaleDateString()}
            </span>
            {domain.verifiedAt && (
              <span className="rounded-full border border-border/70 bg-background/70 px-2.5 py-1">
                Verified {new Date(domain.verifiedAt).toLocaleDateString()}
              </span>
            )}
          </div>
        </CardHeader>
        <CardContent className="space-y-6">
          {domain.failureReason && (
            <div className="rounded-2xl border border-[var(--status-failed-text)]/30 bg-[var(--status-failed-bg)]/70 px-4 py-3 text-sm text-[var(--status-failed-text)]">
              <div className="text-xs uppercase tracking-[0.2em] mb-1">Verification Failed</div>
              {domain.failureReason}
            </div>
          )}

          {domain.status === 'verifying' && (
            <div className="rounded-2xl border border-[var(--status-verifying-text)]/30 bg-[var(--status-verifying-bg)]/70 px-4 py-3 text-sm text-[var(--status-verifying-text)]">
              <div className="text-xs uppercase tracking-[0.2em] mb-1">Verifying</div>
              DNS records are being verified. This may take a few minutes.
            </div>
          )}

          {domain.status === 'pending' && (
            <div className="rounded-2xl border border-border/70 bg-muted/30 px-4 py-3 text-sm text-muted-foreground">
              <div className="text-xs uppercase tracking-[0.2em] mb-1">Pending</div>
              Waiting for DNS setup to begin.
            </div>
          )}

          {hasDnsRecords && (
            <div className="space-y-4">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <h3 className="text-lg font-semibold">DNS Records</h3>
                <DnsActions domain={domain} />
              </div>
              <DnsRecordsTable records={domain.dnsRecords!} />
            </div>
          )}

          {!hasDnsRecords && domain.status !== 'pending' && (
            <div className="rounded-2xl border border-dashed border-border/70 bg-background/60 p-6 text-center text-muted-foreground">
              No DNS records available yet.
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
