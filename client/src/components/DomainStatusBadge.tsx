import { cn } from '@/lib/utils';
import type { DomainStatus } from '@/lib/schemas';

interface DomainStatusBadgeProps {
  status: DomainStatus;
}

const statusStyles: Record<DomainStatus, string> = {
  pending: 'bg-[var(--status-pending-bg)] text-[var(--status-pending-text)]',
  verifying: 'bg-[var(--status-verifying-bg)] text-[var(--status-verifying-text)]',
  verified: 'bg-[var(--status-verified-bg)] text-[var(--status-verified-text)]',
  failed: 'bg-[var(--status-failed-bg)] text-[var(--status-failed-text)]',
};

export function DomainStatusBadge({ status }: DomainStatusBadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold uppercase tracking-wide',
        statusStyles[status]
      )}
    >
      {status}
    </span>
  );
}
