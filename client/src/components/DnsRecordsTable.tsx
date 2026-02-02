import type { DnsRecord } from '@/lib/schemas';

interface DnsRecordsTableProps {
  records: DnsRecord[];
}

export function DnsRecordsTable({ records }: DnsRecordsTableProps) {
  if (records.length === 0) {
    return <p className="text-sm text-muted-foreground">No DNS records configured.</p>;
  }

  return (
    <div className="overflow-x-auto rounded border">
      <table className="w-full text-xs">
        <thead className="bg-muted/50">
          <tr className="border-b text-left">
            <th className="py-2 px-3 font-medium text-muted-foreground">Type</th>
            <th className="py-2 px-3 font-medium text-muted-foreground">Name</th>
            <th className="py-2 px-3 font-medium text-muted-foreground">Value</th>
          </tr>
        </thead>
        <tbody className="font-mono">
          {records.map((record, index) => (
            <tr key={index} className="border-b last:border-0 hover:bg-muted/30">
              <td className="py-2 px-3">
                <span className="inline-block rounded bg-muted px-1.5 py-0.5 text-[10px] font-medium">
                  {record.type}
                </span>
              </td>
              <td className="py-2 px-3 break-all">{record.name}</td>
              <td className="py-2 px-3 break-all text-muted-foreground">{record.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
