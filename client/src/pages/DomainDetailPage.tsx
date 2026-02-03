import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { toast } from 'sonner';
import { ArrowLeft, Trash2, Mail, AlertTriangle } from 'lucide-react';
import { useDomain, useDeleteDomain } from '@/hooks/useDomains';
import { DomainStatusBadge } from '@/components/DomainStatusBadge';
import { DnsRecordsTable } from '@/components/DnsRecordsTable';
import { DnsActions } from '@/components/DnsActions';
import { SendTestEmailForm } from '@/components/SendTestEmailForm';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

interface DeleteConfirmModalProps {
  domainName: string;
  isDeleting: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

interface VerificationStatusProps {
  lastCheckedAt: string | null;
  nextCheckAt: string | null;
}

function VerificationStatus({ lastCheckedAt, nextCheckAt }: VerificationStatusProps) {
  const [timeUntilNext, setTimeUntilNext] = useState<string>('');

  useEffect(() => {
    if (!nextCheckAt) return;

    const updateCountdown = () => {
      const now = new Date();
      const next = new Date(nextCheckAt);
      const diffMs = next.getTime() - now.getTime();

      if (diffMs <= 0) {
        setTimeUntilNext('any moment now');
        return;
      }

      const diffSeconds = Math.floor(diffMs / 1000);
      const minutes = Math.floor(diffSeconds / 60);
      const seconds = diffSeconds % 60;

      if (minutes > 0) {
        setTimeUntilNext(`${minutes}m ${seconds}s`);
      } else {
        setTimeUntilNext(`${seconds}s`);
      }
    };

    updateCountdown();
    const interval = setInterval(updateCountdown, 1000);
    return () => clearInterval(interval);
  }, [nextCheckAt]);

  return (
    <div className="rounded border border-[var(--status-verifying-text)]/20 bg-[var(--status-verifying-bg)] px-3 py-2 text-sm text-[var(--status-verifying-text)]">
      <span className="text-xs uppercase tracking-wide">Verifying</span>
      <p className="mt-1">
        DNS records are being verified. Next check in <span className="font-medium">{timeUntilNext || '...'}</span>
      </p>
      {lastCheckedAt && (
        <p className="mt-0.5 text-xs opacity-75">
          Last checked: {new Date(lastCheckedAt).toLocaleTimeString()}
        </p>
      )}
    </div>
  );
}

function DeleteConfirmModal({ domainName, isDeleting, onConfirm, onCancel }: DeleteConfirmModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/50" onClick={onCancel} />
      <Card className="relative z-10 w-full max-w-sm" role="alertdialog" aria-modal="true">
        <div className="p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-destructive/10">
              <AlertTriangle className="h-5 w-5 text-destructive" />
            </div>
            <div>
              <h3 className="font-display text-lg font-semibold">Delete domain</h3>
              <p className="text-sm text-muted-foreground">This action cannot be undone</p>
            </div>
          </div>
          <p className="mb-4 text-sm">
            Are you sure you want to delete <span className="font-mono font-medium">{domainName}</span>?
            This will also remove the domain from AWS SES and delete all DNS records from Cloudflare.
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onCancel} disabled={isDeleting}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={onConfirm} disabled={isDeleting}>
              {isDeleting ? 'Deleting...' : 'Delete'}
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
}

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
  const [showDeleteModal, setShowDeleteModal] = useState(false);

  const handleDelete = async () => {
    if (!domain) return;

    try {
      await deleteMutation.mutateAsync(domain.id);
      toast.success('Domain deleted');
      navigate('/');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to delete domain');
      setShowDeleteModal(false);
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
                onClick={() => setShowDeleteModal(true)}
                className="text-destructive hover:text-destructive hover:bg-destructive/10"
              >
                <Trash2 className="h-3.5 w-3.5 mr-1.5" />
                Delete
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
            <VerificationStatus
              lastCheckedAt={domain.lastCheckedAt}
              nextCheckAt={domain.nextCheckAt}
            />
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

      {showDeleteModal && (
        <DeleteConfirmModal
          domainName={domain.name}
          isDeleting={deleteMutation.isPending}
          onConfirm={handleDelete}
          onCancel={() => setShowDeleteModal(false)}
        />
      )}
    </div>
  );
}
