import { useRef, useState } from 'react';
import { Download, Upload } from 'lucide-react';
import { useExportData, useImportData } from '@/hooks/useDataTransfer';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import type { ImportResult } from '@/lib/schemas';

function ErrorText({ message }: { message: string }) {
  return (
    <p className="mt-3 rounded border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">
      {message}
    </p>
  );
}

function ImportSummary({ result }: { result: ImportResult }) {
  const rows: [string, number][] = [
    ['Domains', result.domains],
    ['API keys', result.apiKeys],
    ['API key domain links', result.apiKeyDomains],
    ['Revoked API keys', result.revokedApiKeys],
    ['Audit logs', result.auditLogs],
    ['Audit logs skipped', result.auditLogsSkipped],
  ];
  return (
    <div className="mt-3 rounded border bg-muted/30 p-3 text-sm">
      <p className="font-medium mb-2">Import complete</p>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1 max-w-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="font-mono text-right">{value}</dd>
          </div>
        ))}
      </dl>
      {result.pendingDomainsRequeued > 0 && (
        <p className="mt-2 text-muted-foreground">
          {result.pendingDomainsRequeued} pending domain(s) were re-queued for setup.
        </p>
      )}
    </div>
  );
}

export function DataPage() {
  const exportData = useExportData();
  const importData = useImportData();
  const [file, setFile] = useState<File | null>(null);
  const [confirming, setConfirming] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const reset = () => {
    setFile(null);
    setConfirming(false);
    if (inputRef.current) inputRef.current.value = '';
  };

  const handleImport = () => {
    if (!file) return;
    importData.mutate(file, { onSettled: reset });
  };

  return (
    <div className="container mx-auto max-w-4xl px-4 py-8 space-y-6">
      <h2 className="font-display text-xl font-semibold">Data</h2>

      <Card className="p-6">
        <h3 className="font-display font-semibold mb-1">Export</h3>
        <p className="text-sm text-muted-foreground mb-4">
          Download domains, API keys (hashed, so existing clients keep working), revoked keys and
          audit logs as a compressed file. Sent emails are not included.
        </p>
        <Button onClick={() => exportData.mutate()} disabled={exportData.isPending}>
          <Download className="h-4 w-4 mr-2" />
          {exportData.isPending ? 'Preparing...' : 'Download export'}
        </Button>
        {exportData.isError && <ErrorText message={exportData.error.message} />}
      </Card>

      <Card className="p-6">
        <h3 className="font-display font-semibold mb-1">Import</h3>
        <p className="text-sm text-muted-foreground mb-4">
          Restore an export into a fresh instance. Only works while there are no domains or API
          keys. Imports domains, API keys and audit logs; sent emails are not imported.
        </p>
        <input
          ref={inputRef}
          type="file"
          accept=".gz,.json"
          disabled={importData.isPending}
          onChange={(e) => {
            setFile(e.target.files?.[0] ?? null);
            setConfirming(false);
            importData.reset();
          }}
          className="block text-sm"
        />
        {file && !confirming && (
          <Button className="mt-4" onClick={() => setConfirming(true)}>
            <Upload className="h-4 w-4 mr-2" />
            Import
          </Button>
        )}
        {file && confirming && (
          <div className="mt-4 rounded border border-amber-500/30 bg-amber-500/5 p-3 text-sm">
            <p className="mb-3">
              Import <span className="font-mono">{file.name}</span> into this instance? This
              cannot be undone.
            </p>
            <div className="flex gap-2">
              <Button onClick={handleImport} disabled={importData.isPending}>
                {importData.isPending ? 'Importing...' : 'Confirm import'}
              </Button>
              <Button
                variant="ghost"
                onClick={() => setConfirming(false)}
                disabled={importData.isPending}
              >
                Cancel
              </Button>
            </div>
          </div>
        )}
        {importData.isError && <ErrorText message={importData.error.message} />}
        {importData.isSuccess && <ImportSummary result={importData.data} />}
      </Card>
    </div>
  );
}
