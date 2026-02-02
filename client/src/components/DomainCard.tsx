import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { DomainStatusBadge } from './DomainStatusBadge';
import { cn } from '@/lib/utils';
import type { Domain } from '@/lib/schemas';

interface DomainCardProps {
  domain: Domain;
}

export function DomainCard({ domain }: DomainCardProps) {
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

        <Link
          to={`/domains/${domain.id}`}
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
        >
          Details
          <ChevronRight className="h-3 w-3" />
        </Link>
      </CardContent>
    </Card>
  );
}
