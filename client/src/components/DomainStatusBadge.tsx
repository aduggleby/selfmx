import { cn } from '@/lib/utils';
import type { DomainStatus } from '@/lib/schemas';

interface DomainStatusBadgeProps {
  status: DomainStatus;
}

const statusStyles: Record<DomainStatus, string> = {
  pending: 'bg-yellow-100 text-yellow-800',
  verifying: 'bg-blue-100 text-blue-800',
  verified: 'bg-green-100 text-green-800',
  failed: 'bg-red-100 text-red-800',
};

export function DomainStatusBadge({ status }: DomainStatusBadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium capitalize',
        statusStyles[status]
      )}
    >
      {status}
    </span>
  );
}
