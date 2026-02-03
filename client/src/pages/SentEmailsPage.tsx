import { useState, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Mail, X, ChevronDown } from 'lucide-react';
import DOMPurify from 'dompurify';
import { useSentEmails, useSentEmail, type SentEmailFilters } from '@/hooks/useSentEmails';
import { useDomains } from '@/hooks/useDomains';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import type { SentEmailListItem, SentEmailDetail, Domain } from '@/lib/schemas';

interface SentEmailDetailModalProps {
  email: SentEmailDetail;
  onClose: () => void;
}

function SentEmailRowSkeleton() {
  return (
    <tr className="border-b">
      <td className="py-3 px-2"><Skeleton className="h-4 w-32" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-40" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-36" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-48" /></td>
    </tr>
  );
}

function sanitizeEmailHtml(html: string): string {
  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'p', 'br', 'span', 'div', 'a', 'b', 'i', 'u', 'strong', 'em',
      'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'ul', 'ol', 'li',
      'table', 'thead', 'tbody', 'tr', 'td', 'th',
      'img', 'hr', 'blockquote', 'pre', 'code',
    ],
    ALLOWED_ATTR: [
      'href', 'src', 'alt', 'title', 'style', 'class',
      'width', 'height', 'border', 'cellpadding', 'cellspacing',
      'align', 'valign', 'bgcolor', 'color',
    ],
    ALLOW_DATA_ATTR: false,
    ADD_ATTR: ['target'],
    FORBID_TAGS: ['script', 'style', 'iframe', 'form', 'input', 'object', 'embed'],
    FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover'],
  });
}

function EmailHtmlPreview({ html }: { html: string }) {
  const sanitizedHtml = sanitizeEmailHtml(html);

  const fullHtml = `
    <!DOCTYPE html>
    <html>
      <head>
        <meta http-equiv="Content-Security-Policy"
              content="default-src 'none'; img-src https: data:; style-src 'unsafe-inline';">
        <style>
          body { font-family: system-ui, sans-serif; font-size: 14px; line-height: 1.5; margin: 16px; }
          img { max-width: 100%; height: auto; }
          a { color: #0066cc; }
        </style>
      </head>
      <body dir="auto">${sanitizedHtml}</body>
    </html>
  `;

  return (
    <iframe
      sandbox="allow-same-origin"
      srcDoc={fullHtml}
      className="w-full h-96 border rounded-md bg-white"
      title="Email HTML preview"
    />
  );
}

function SentEmailDetailModal({ email, onClose }: SentEmailDetailModalProps) {
  const [activeTab, setActiveTab] = useState<'html' | 'text'>(email.htmlBody ? 'html' : 'text');

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleString();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <Card className="relative z-10 w-full max-w-2xl max-h-[90vh] overflow-hidden flex flex-col" role="dialog" aria-modal="true">
        <div className="p-5 border-b flex items-start justify-between shrink-0">
          <div className="min-w-0 flex-1">
            <h3 className="font-display text-lg font-semibold truncate">{email.subject}</h3>
            <p className="text-xs text-muted-foreground mt-1">
              {formatDate(email.sentAt)} · {email.messageId}
            </p>
          </div>
          <Button variant="ghost" size="sm" onClick={onClose} className="shrink-0 ml-2">
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="p-5 space-y-3 border-b shrink-0">
          <div className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
            <span className="text-muted-foreground">From:</span>
            <span className="font-mono text-xs">{email.fromAddress}</span>

            <span className="text-muted-foreground">To:</span>
            <span className="font-mono text-xs">{email.to.join(', ')}</span>

            {email.cc && email.cc.length > 0 && (
              <>
                <span className="text-muted-foreground">CC:</span>
                <span className="font-mono text-xs">{email.cc.join(', ')}</span>
              </>
            )}

            {email.replyTo && (
              <>
                <span className="text-muted-foreground">Reply-To:</span>
                <span className="font-mono text-xs">{email.replyTo}</span>
              </>
            )}
          </div>
        </div>

        <div className="border-b shrink-0">
          <div className="flex gap-1 p-2" role="tablist">
            {email.htmlBody && (
              <button
                role="tab"
                aria-selected={activeTab === 'html'}
                onClick={() => setActiveTab('html')}
                className={`px-3 py-1.5 text-sm rounded transition-colors ${
                  activeTab === 'html'
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted'
                }`}
              >
                HTML
              </button>
            )}
            {email.textBody && (
              <button
                role="tab"
                aria-selected={activeTab === 'text'}
                onClick={() => setActiveTab('text')}
                className={`px-3 py-1.5 text-sm rounded transition-colors ${
                  activeTab === 'text'
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted'
                }`}
              >
                Text
              </button>
            )}
          </div>
        </div>

        <div className="flex-1 overflow-auto p-5" role="tabpanel">
          {activeTab === 'html' && email.htmlBody && (
            <EmailHtmlPreview html={email.htmlBody} />
          )}
          {activeTab === 'text' && email.textBody && (
            <pre className="whitespace-pre-wrap font-mono text-sm bg-muted p-4 rounded">
              {email.textBody}
            </pre>
          )}
          {!email.htmlBody && !email.textBody && (
            <p className="text-sm text-muted-foreground text-center py-8">
              No email body available.
            </p>
          )}
        </div>
      </Card>
    </div>
  );
}

function SentEmailRow({
  email,
  onClick,
}: {
  email: SentEmailListItem;
  onClick: () => void;
}) {
  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    const now = new Date();
    const isToday = date.toDateString() === now.toDateString();

    if (isToday) {
      return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }
    return date.toLocaleDateString();
  };

  const formatRecipients = (to: string[]) => {
    if (to.length === 1) return to[0];
    return `${to[0]} +${to.length - 1}`;
  };

  return (
    <tr
      className="border-b hover:bg-muted/50 cursor-pointer transition-colors"
      onClick={onClick}
    >
      <td className="py-3 px-2 text-xs text-muted-foreground whitespace-nowrap">
        {formatDate(email.sentAt)}
      </td>
      <td className="py-3 px-2 font-mono text-xs truncate max-w-[200px]">
        {email.fromAddress}
      </td>
      <td className="py-3 px-2 font-mono text-xs truncate max-w-[200px]">
        {formatRecipients(email.to)}
      </td>
      <td className="py-3 px-2 truncate max-w-[250px]">
        {email.subject}
      </td>
    </tr>
  );
}

function EmptySentEmails({ hasFilters }: { hasFilters: boolean }) {
  return (
    <div className="flex flex-col items-center justify-center py-12 text-center">
      <Mail className="h-12 w-12 text-muted-foreground/50 mb-4" />
      <h3 className="text-lg font-medium">
        {hasFilters ? 'No emails match your filters' : 'No emails sent yet'}
      </h3>
      <p className="text-sm text-muted-foreground mt-1 max-w-sm">
        {hasFilters
          ? 'Try adjusting your filters or clearing them to see all emails.'
          : 'Emails sent through the API will appear here.'}
      </p>
    </div>
  );
}

interface FiltersBarProps {
  filters: SentEmailFilters;
  domains: Domain[];
  onFilterChange: (filters: Partial<SentEmailFilters>) => void;
  onClear: () => void;
}

function FiltersBar({ filters, domains, onFilterChange, onClear }: FiltersBarProps) {
  const verifiedDomains = domains.filter((d) => d.status === 'verified');
  const hasFilters = !!(filters.domainId || filters.from || filters.to);

  const datePresets = [
    { label: 'Today', days: 0 },
    { label: '7 days', days: 7 },
    { label: '30 days', days: 30 },
  ];

  const handlePreset = (days: number) => {
    const now = new Date();
    const to = now.toISOString().split('T')[0];
    const fromDate = new Date(now.getTime() - days * 86400000);
    const from = fromDate.toISOString().split('T')[0];
    onFilterChange({ from, to });
  };

  return (
    <div className="flex flex-wrap gap-2 mb-4">
      <div className="relative">
        <select
          value={filters.domainId || ''}
          onChange={(e) => onFilterChange({ domainId: e.target.value || undefined })}
          className="h-9 rounded-md border border-input bg-background px-3 pr-8 text-sm appearance-none cursor-pointer"
        >
          <option value="">All domains</option>
          {verifiedDomains.map((domain) => (
            <option key={domain.id} value={domain.id}>
              {domain.name}
            </option>
          ))}
        </select>
        <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
      </div>

      <Input
        type="text"
        placeholder="From address"
        value={filters.from || ''}
        onChange={(e) => onFilterChange({ from: e.target.value || undefined })}
        className="h-9 w-40"
      />

      <Input
        type="text"
        placeholder="To address"
        value={filters.to || ''}
        onChange={(e) => onFilterChange({ to: e.target.value || undefined })}
        className="h-9 w-40"
      />

      <div className="flex gap-1" role="group" aria-label="Date presets">
        {datePresets.map(({ label, days }) => (
          <Button
            key={label}
            variant="outline"
            size="sm"
            onClick={() => handlePreset(days)}
            className="h-9"
          >
            {label}
          </Button>
        ))}
      </div>

      {hasFilters && (
        <Button
          variant="ghost"
          size="sm"
          onClick={onClear}
          className="h-9"
          aria-label="Clear all filters"
        >
          <X className="h-4 w-4 mr-1" />
          Clear
        </Button>
      )}
    </div>
  );
}

export function SentEmailsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [selectedEmailId, setSelectedEmailId] = useState<string | null>(null);

  // Get filters from URL
  const filters: SentEmailFilters = useMemo(() => ({
    domainId: searchParams.get('domainId') || undefined,
    from: searchParams.get('from') || undefined,
    to: searchParams.get('to') || undefined,
    pageSize: 50,
  }), [searchParams]);

  const setFilters = (newFilters: Partial<SentEmailFilters>) => {
    const params = new URLSearchParams(searchParams);
    Object.entries(newFilters).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params.set(key, String(value));
      } else {
        params.delete(key);
      }
    });
    setSearchParams(params, { replace: true });
  };

  const clearFilters = () => setSearchParams({}, { replace: true });

  const {
    data,
    isLoading,
    error,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useSentEmails(filters);

  const { data: selectedEmail, isLoading: isLoadingDetail } = useSentEmail(selectedEmailId);
  const { data: domainsData } = useDomains(1, 100);

  const emails = data?.pages.flatMap((page) => page.data) ?? [];
  const domains = domainsData?.data ?? [];
  const hasEmails = emails.length > 0;
  const hasFiltersApplied = !!(filters.domainId || filters.from || filters.to);

  return (
    <div className="container mx-auto max-w-4xl px-4 py-8">
      <FiltersBar
        filters={filters}
        domains={domains}
        onFilterChange={setFilters}
        onClear={clearFilters}
      />

      {isLoading && (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs text-muted-foreground">
                  <th className="py-2 px-2 font-medium">Date</th>
                  <th className="py-2 px-2 font-medium">From</th>
                  <th className="py-2 px-2 font-medium">To</th>
                  <th className="py-2 px-2 font-medium">Subject</th>
                </tr>
              </thead>
              <tbody>
                {[...Array(10)].map((_, i) => (
                  <SentEmailRowSkeleton key={i} />
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {error && (
        <div className="rounded border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          <span className="font-medium">Error:</span> {error.message}
        </div>
      )}

      {!isLoading && !error && !hasEmails && (
        <EmptySentEmails hasFilters={hasFiltersApplied} />
      )}

      {!isLoading && !error && hasEmails && (
        <>
          <Card>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-xs text-muted-foreground">
                    <th className="py-2 px-2 font-medium">Date</th>
                    <th className="py-2 px-2 font-medium">From</th>
                    <th className="py-2 px-2 font-medium">To</th>
                    <th className="py-2 px-2 font-medium">Subject</th>
                  </tr>
                </thead>
                <tbody>
                  {emails.map((email) => (
                    <SentEmailRow
                      key={email.id}
                      email={email}
                      onClick={() => setSelectedEmailId(email.id)}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {hasNextPage && (
            <div className="mt-6 flex justify-center">
              <Button
                variant="outline"
                onClick={() => fetchNextPage()}
                disabled={isFetchingNextPage}
              >
                {isFetchingNextPage ? 'Loading...' : 'Load More'}
              </Button>
            </div>
          )}
        </>
      )}

      {selectedEmailId && selectedEmail && !isLoadingDetail && (
        <SentEmailDetailModal
          email={selectedEmail}
          onClose={() => setSelectedEmailId(null)}
        />
      )}
    </div>
  );
}
