import type { DnsRecord } from '@/lib/schemas';

interface DnsRecordsTableProps {
  records: DnsRecord[];
}

export function DnsRecordsTable({ records }: DnsRecordsTableProps) {
  if (records.length === 0) {
    return <p className="text-sm text-muted-foreground">No DNS records configured.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b">
            <th className="text-left py-2 px-2 font-medium">Type</th>
            <th className="text-left py-2 px-2 font-medium">Name</th>
            <th className="text-left py-2 px-2 font-medium">Value</th>
          </tr>
        </thead>
        <tbody>
          {records.map((record, index) => (
            <tr key={index} className="border-b last:border-0">
              <td className="py-2 px-2">
                <code className="bg-muted px-1 rounded text-xs">{record.type}</code>
              </td>
              <td className="py-2 px-2">
                <code className="text-xs break-all">{record.name}</code>
              </td>
              <td className="py-2 px-2">
                <code className="text-xs break-all">{record.value}</code>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
