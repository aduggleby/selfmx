import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
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
        'group hover:border-primary/30',
        'motion-safe:animate-[fade-slide_0.2s_ease-out]'
      )}
    >
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between gap-3">
          <Link
            to={`/domains/${domain.id}`}
            className="font-mono text-sm font-medium hover:text-primary transition-colors"
          >
            {domain.name}
          </Link>
          <DomainStatusBadge status={domain.status} />
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
      </CardHeader>
      <CardContent>
        {domain.failureReason && (
          <p className="mb-3 rounded border border-[var(--status-failed-text)]/20 bg-[var(--status-failed-bg)] px-3 py-2 text-xs text-[var(--status-failed-text)]">
            {domain.failureReason}
          </p>
        )}

        {domain.status === 'verifying' && (
          <p className="mb-3 rounded border border-[var(--status-verifying-text)]/20 bg-[var(--status-verifying-bg)] px-3 py-2 text-xs text-[var(--status-verifying-text)]">
            Verifying DNS records...
          </p>
        )}

        {hasDnsRecords && (
          <div className="mb-3">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowDns(!showDns)}
              className="h-7 px-2 text-xs text-muted-foreground hover:text-foreground"
            >
              {showDns ? '− Hide DNS' : '+ Show DNS'}
            </Button>
            {showDns && (
              <div className="mt-2">
                <DnsRecordsTable records={domain.dnsRecords!} />
              </div>
            )}
          </div>
        )}

        <Button
          variant="ghost"
          size="sm"
          onClick={() => onDelete(domain.id)}
          disabled={isDeleting}
          className="h-7 px-2 text-xs text-destructive hover:text-destructive hover:bg-destructive/10"
        >
          {isDeleting ? 'Deleting...' : 'Delete'}
        </Button>
      </CardContent>
    </Card>
  );
}
