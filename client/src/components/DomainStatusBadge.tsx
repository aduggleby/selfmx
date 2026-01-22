import { cn } from '@/lib/utils';
import type { DomainStatus } from '@/lib/schemas';

interface DomainStatusBadgeProps {
  status: DomainStatus;
}

const statusStyles: Record<DomainStatus, string> = {
  pending: cn(
    'bg-[var(--status-pending-bg)] text-[var(--status-pending-text)]',
    'ring-1 ring-inset ring-[var(--status-pending-text)]/20'
  ),
  verifying: cn(
    'bg-[var(--status-verifying-bg)] text-[var(--status-verifying-text)]',
    'ring-1 ring-inset ring-[var(--status-verifying-text)]/20',
    'relative overflow-hidden'
  ),
  verified: cn(
    'bg-[var(--status-verified-bg)] text-[var(--status-verified-text)]',
    'ring-1 ring-inset ring-[var(--status-verified-text)]/20'
  ),
  failed: cn(
    'bg-[var(--status-failed-bg)] text-[var(--status-failed-text)]',
    'ring-1 ring-inset ring-[var(--status-failed-text)]/20'
  ),
};

export function DomainStatusBadge({ status }: DomainStatusBadgeProps) {
  if (status === 'verifying') {
    return (
      <span
        className={cn(
          'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold uppercase tracking-wide',
          statusStyles[status]
        )}
      >
        <span className="absolute inset-0 rounded-full motion-safe:animate-[pulse-ring_2s_ease-in-out_infinite] bg-[var(--status-verifying-text)]/10" />
        <span className="relative">{status}</span>
      </span>
    );
  }

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
