import { Download, ExternalLink } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { generateBindFile, downloadBindFile } from '@/lib/dns';
import type { Domain } from '@/lib/schemas';

interface DnsActionsProps {
  domain: Domain;
}

export function DnsActions({ domain }: DnsActionsProps) {
  const handleDownload = () => {
    if (!domain.dnsRecords) return;
    const content = generateBindFile(domain.name, domain.dnsRecords);
    downloadBindFile(domain.name, content);
  };

  const handleOpenCloudflare = () => {
    const url = `https://dash.cloudflare.com/?to=/:account/${domain.name}/dns`;
    window.open(url, '_blank');
  };

  const hasRecords = domain.dnsRecords && domain.dnsRecords.length > 0;

  return (
    <div className="flex flex-wrap gap-2">
      <Button
        variant="outline"
        size="sm"
        onClick={handleDownload}
        disabled={!hasRecords}
      >
        <Download className="h-3.5 w-3.5 mr-1.5" />
        Download
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={handleOpenCloudflare}
      >
        <ExternalLink className="h-3.5 w-3.5 mr-1.5" />
        Cloudflare
      </Button>
    </div>
  );
}
