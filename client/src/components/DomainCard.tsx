import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { DomainStatusBadge } from './DomainStatusBadge';
import { DnsRecordsTable } from './DnsRecordsTable';
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
    <Card className="hover:shadow-lg hover:shadow-blue-500/10 transition-shadow duration-200">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-lg font-medium">{domain.name}</CardTitle>
        <DomainStatusBadge status={domain.status} />
      </CardHeader>
      <CardContent>
        <div className="text-sm text-muted-foreground mb-4">
          Created: {new Date(domain.createdAt).toLocaleDateString()}
          {domain.verifiedAt && (
            <span className="ml-4">
              Verified: {new Date(domain.verifiedAt).toLocaleDateString()}
            </span>
          )}
        </div>

        {domain.failureReason && (
          <p className="text-sm text-[var(--status-failed-text)] mb-4">{domain.failureReason}</p>
        )}

        {domain.status === 'verifying' && (
          <p className="text-sm text-[var(--status-verifying-text)] mb-4">
            DNS records are being verified. This may take a few minutes.
          </p>
        )}

        {hasDnsRecords && (
          <div className="mb-4">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowDns(!showDns)}
              className="mb-2"
            >
              {showDns ? 'Hide DNS Records' : 'Show DNS Records'}
            </Button>
            {showDns && <DnsRecordsTable records={domain.dnsRecords!} />}
          </div>
        )}

        <Button
          variant="destructive"
          size="sm"
          onClick={() => onDelete(domain.id)}
          disabled={isDeleting}
        >
          {isDeleting ? 'Deleting...' : 'Delete'}
        </Button>
      </CardContent>
    </Card>
  );
}
