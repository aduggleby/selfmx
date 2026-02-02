import { useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { toast } from 'sonner';
import { ArrowLeft, Trash2, Mail } from 'lucide-react';
import { useDomain, useDeleteDomain } from '@/hooks/useDomains';
import { DomainStatusBadge } from '@/components/DomainStatusBadge';
import { DnsRecordsTable } from '@/components/DnsRecordsTable';
import { DnsActions } from '@/components/DnsActions';
import { SendTestEmailForm } from '@/components/SendTestEmailForm';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

function DomainDetailSkeleton() {
  return (
    <div className="container mx-auto max-w-4xl px-4 py-8">
      <Skeleton className="h-4 w-28 mb-6" />
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-5 w-16 rounded" />
          </div>
        </CardHeader>
        <CardContent>
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
  const [showTestEmailForm, setShowTestEmailForm] = useState(false);

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
      <div className="container mx-auto max-w-4xl px-4 py-8">
        <Link
          to="/"
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors mb-6"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          Back
        </Link>
        <Card className="border-destructive/20">
          <CardHeader>
            <h2 className="text-lg font-semibold text-destructive">Domain not found</h2>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              {error?.message ?? 'The requested domain could not be found.'}
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  const hasDnsRecords = domain.dnsRecords && domain.dnsRecords.length > 0;

  return (
    <div className="container mx-auto max-w-4xl px-4 py-8">
      <Link
        to="/"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors mb-6"
      >
        <ArrowLeft className="h-3.5 w-3.5" />
        Back
      </Link>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3">
              <h2 className="font-mono text-lg font-medium">{domain.name}</h2>
              <DomainStatusBadge status={domain.status} />
            </div>
            <div className="flex gap-2">
              {domain.status === 'verified' && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setShowTestEmailForm(!showTestEmailForm)}
                >
                  <Mail className="h-3.5 w-3.5 mr-1.5" />
                  Test
                </Button>
              )}
              <Button
                variant="ghost"
                size="sm"
                onClick={handleDelete}
                disabled={deleteMutation.isPending}
                className="text-destructive hover:text-destructive hover:bg-destructive/10"
              >
                <Trash2 className="h-3.5 w-3.5 mr-1.5" />
                {deleteMutation.isPending ? 'Deleting...' : 'Delete'}
              </Button>
            </div>
          </div>
          <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
            <span>Added {new Date(domain.createdAt).toLocaleDateString()}</span>
            {domain.verifiedAt && (
              <>
                <span>·</span>
                <span>Verified {new Date(domain.verifiedAt).toLocaleDateString()}</span>
              </>
            )}
          </div>

          {showTestEmailForm && (
            <div className="mt-4">
              <SendTestEmailForm
                domainId={domain.id}
                domainName={domain.name}
                onClose={() => setShowTestEmailForm(false)}
              />
            </div>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          {domain.failureReason && (
            <div className="rounded border border-[var(--status-failed-text)]/20 bg-[var(--status-failed-bg)] px-3 py-2 text-sm text-[var(--status-failed-text)]">
              <span className="text-xs uppercase tracking-wide">Verification Failed</span>
              <p className="mt-1">{domain.failureReason}</p>
            </div>
          )}

          {domain.status === 'verifying' && (
            <div className="rounded border border-[var(--status-verifying-text)]/20 bg-[var(--status-verifying-bg)] px-3 py-2 text-sm text-[var(--status-verifying-text)]">
              <span className="text-xs uppercase tracking-wide">Verifying</span>
              <p className="mt-1">DNS records are being verified. This may take a few minutes.</p>
            </div>
          )}

          {domain.status === 'pending' && (
            <div className="rounded border bg-muted/50 px-3 py-2 text-sm text-muted-foreground">
              <span className="text-xs uppercase tracking-wide">Pending</span>
              <p className="mt-1">Waiting for DNS setup to begin.</p>
            </div>
          )}

          {hasDnsRecords && (
            <div className="space-y-3">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <h3 className="text-sm font-medium">DNS Records</h3>
                <DnsActions domain={domain} />
              </div>
              <DnsRecordsTable records={domain.dnsRecords!} />
            </div>
          )}

          {!hasDnsRecords && domain.status !== 'pending' && (
            <div className="rounded border border-dashed bg-muted/30 p-4 text-center text-sm text-muted-foreground">
              No DNS records available yet.
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
