import type { DnsRecord } from '@/lib/schemas';

interface DnsRecordsTableProps {
  records: DnsRecord[];
}

export function DnsRecordsTable({ records }: DnsRecordsTableProps) {
  if (records.length === 0) {
    return <p className="text-sm text-muted-foreground">No DNS records configured.</p>;
  }

  return (
    <div className="overflow-hidden rounded-2xl border border-border/70 bg-background/70">
      <table className="w-full text-sm">
        <thead className="bg-muted/60">
          <tr className="border-b border-border/60 text-xs uppercase tracking-[0.15em] text-muted-foreground">
            <th className="py-3 px-3 text-left font-medium">Type</th>
            <th className="py-3 px-3 text-left font-medium">Name</th>
            <th className="py-3 px-3 text-left font-medium">Value</th>
          </tr>
        </thead>
        <tbody>
          {records.map((record, index) => (
            <tr
              key={index}
              className="border-b border-border/60 last:border-0 hover:bg-muted/40 transition-colors"
            >
              <td className="py-3 px-3">
                <code className="rounded-full bg-muted px-2 py-1 text-xs">{record.type}</code>
              </td>
              <td className="py-3 px-3">
                <code className="text-xs break-all">{record.name}</code>
              </td>
              <td className="py-3 px-3">
                <code className="text-xs break-all">{record.value}</code>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
