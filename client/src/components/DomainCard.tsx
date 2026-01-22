import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { DomainStatusBadge } from './DomainStatusBadge';
import { DnsRecordsTable } from './DnsRecordsTable';
import { cn } from '@/lib/utils';
import type { Domain } from '@/lib/schemas';

interface DomainCardProps {
  domain: Domain;
  onDelete: (id: string) => void;
  isDeleting: boolean;
}

export function DomainCard({ domain, onDelete, isDeleting }: DomainCardProps) {
  const [showDns, setShowDns] = useState(false);
  const hasDnsRecords = domain.dnsRecords && domain.dnsRecords.length > 0;

  return (
    <Card
      className={cn(
        'group relative overflow-hidden border-border/70',
        'motion-safe:animate-[fade-slide_0.35s_ease-out]',
        'hover:shadow-[var(--shadow-elevation-high)]',
        'hover:-translate-y-1'
      )}
    >
      <div className="absolute inset-x-0 top-0 h-1 bg-primary" />
      <CardHeader className="flex flex-col gap-3 pb-3">
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="text-lg font-semibold">{domain.name}</CardTitle>
          <DomainStatusBadge status={domain.status} />
        </div>
        <div className="flex flex-wrap gap-3 text-xs text-muted-foreground">
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
      <CardContent>

        {domain.failureReason && (
          <p className="mb-4 rounded-2xl border border-[var(--status-failed-text)]/30 bg-[var(--status-failed-bg)]/70 px-4 py-3 text-sm text-[var(--status-failed-text)]">
            {domain.failureReason}
          </p>
        )}

        {domain.status === 'verifying' && (
          <p className="mb-4 rounded-2xl border border-[var(--status-verifying-text)]/30 bg-[var(--status-verifying-bg)]/70 px-4 py-3 text-sm text-[var(--status-verifying-text)]">
            DNS records are being verified. This may take a few minutes.
          </p>
        )}

        {hasDnsRecords && (
          <div className="mb-5">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowDns(!showDns)}
              className="mb-3 rounded-full px-4"
            >
              {showDns ? 'Hide DNS Records' : 'Show DNS Records'}
            </Button>
            <div
              className={cn(
                'grid transition-all duration-200 ease-out',
                showDns ? 'grid-rows-[1fr] opacity-100 visible' : 'grid-rows-[0fr] opacity-0 invisible'
              )}
            >
              <div className="overflow-hidden">
                <DnsRecordsTable records={domain.dnsRecords!} />
              </div>
            </div>
          </div>
        )}

        <Button
          variant="destructive"
          size="sm"
          onClick={() => onDelete(domain.id)}
          disabled={isDeleting}
          className="rounded-full px-4"
        >
          {isDeleting ? 'Deleting...' : 'Delete'}
        </Button>
      </CardContent>
    </Card>
  );
}
