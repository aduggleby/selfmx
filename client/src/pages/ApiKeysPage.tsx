import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Key, Plus, Trash2, AlertTriangle, Copy, Check, ChevronDown, ChevronRight, Archive, Pencil, X, ArrowUp, ArrowDown, ArrowUpDown } from 'lucide-react';
import { useApiKeys, useApiKeyStats, useCreateApiKey, useDeleteApiKey, useRenameApiKey, useRevokedApiKeys } from '@/hooks/useApiKeys';
import { useDomains } from '@/hooks/useDomains';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import type { ApiKey, ApiKeyCreated, ApiKeyStats, Domain, RevokedApiKey } from '@/lib/schemas';
import { formatDateTime } from '@/lib/date';

interface CreateApiKeyModalProps {
  domains: Domain[];
  isCreating: boolean;
  onSubmit: (name: string, domainIds: string[], isAdmin: boolean) => void;
  onCancel: () => void;
}

interface ApiKeyRevealModalProps {
  apiKey: ApiKeyCreated;
  onClose: () => void;
}

interface DeleteApiKeyModalProps {
  apiKey: ApiKey;
  isDeleting: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

function ApiKeyRowSkeleton() {
  return (
    <tr className="border-b">
      <td className="py-3 px-2"><Skeleton className="h-4 w-32" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-24 font-mono" /></td>
      <td className="py-3 px-2"><Skeleton className="h-5 w-16" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-20" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-20" /></td>
      {STAT_WINDOWS.map((w) => (
        <td key={w.key} className="py-3 px-2"><Skeleton className="ml-auto h-4 w-8" /></td>
      ))}
      <td className="py-3 px-2"><Skeleton className="h-8 w-16" /></td>
    </tr>
  );
}

function CreateApiKeyModal({ domains, isCreating, onSubmit, onCancel }: CreateApiKeyModalProps) {
  const [name, setName] = useState('');
  const [isAdmin, setIsAdmin] = useState(false);
  const [selectedDomainIds, setSelectedDomainIds] = useState<string[]>([]);

  const verifiedDomains = domains.filter((d) => d.status === 'verified');
  const canSubmit = name.trim() && (isAdmin || selectedDomainIds.length > 0);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    onSubmit(name.trim(), isAdmin ? [] : selectedDomainIds, isAdmin);
  };

  const toggleDomain = (domainId: string) => {
    setSelectedDomainIds((prev) =>
      prev.includes(domainId) ? prev.filter((id) => id !== domainId) : [...prev, domainId]
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/50" onClick={onCancel} />
      <Card className="relative z-10 w-full max-w-md" role="dialog" aria-modal="true">
        <div className="p-5">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="font-display text-lg font-semibold">Create API Key</h3>
            <Button variant="ghost" size="sm" onClick={onCancel}>
              Close
            </Button>
          </div>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="api-key-name" className="text-sm font-medium block mb-1.5">
                Name
              </label>
              <Input
                id="api-key-name"
                placeholder="e.g., Production, Staging"
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoFocus
              />
            </div>

            <div>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={isAdmin}
                  onChange={(e) => setIsAdmin(e.target.checked)}
                  className="h-4 w-4 rounded border-input"
                />
                <span className="font-medium">Admin key</span>
                <span className="text-muted-foreground">(access to all domains)</span>
              </label>
            </div>

            {!isAdmin && (
              <div>
                <label className="text-sm font-medium block mb-1.5">
                  Domains
                </label>
                {verifiedDomains.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    No verified domains available. Verify a domain first.
                  </p>
                ) : (
                  <div className="space-y-1.5 max-h-40 overflow-y-auto rounded border p-2">
                    {verifiedDomains.map((domain) => (
                      <label key={domain.id} className="flex items-center gap-2 text-sm">
                        <input
                          type="checkbox"
                          checked={selectedDomainIds.includes(domain.id)}
                          onChange={() => toggleDomain(domain.id)}
                          className="h-4 w-4 rounded border-input"
                        />
                        <span className="font-mono text-xs">{domain.name}</span>
                      </label>
                    ))}
                  </div>
                )}
              </div>
            )}

            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" type="button" onClick={onCancel} disabled={isCreating}>
                Cancel
              </Button>
              <Button type="submit" disabled={isCreating || !canSubmit}>
                {isCreating ? 'Creating...' : 'Create'}
              </Button>
            </div>
          </form>
        </div>
      </Card>
    </div>
  );
}

function ApiKeyRevealModal({ apiKey, onClose }: ApiKeyRevealModalProps) {
  const [copied, setCopied] = useState(false);
  const [confirmed, setConfirmed] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(apiKey.key);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback for older browsers
      const textarea = document.createElement('textarea');
      textarea.value = apiKey.key;
      textarea.style.position = 'fixed';
      textarea.style.opacity = '0';
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/50" />
      <Card className="relative z-10 w-full max-w-md" role="alertdialog" aria-modal="true">
        <div className="p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-green-500/10">
              <Key className="h-5 w-5 text-green-600" />
            </div>
            <div>
              <h3 className="font-display text-lg font-semibold">API Key Created</h3>
              <p className="text-sm text-muted-foreground">{apiKey.name}</p>
            </div>
          </div>

          <div className="rounded border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-700 dark:text-amber-400 mb-4">
            <strong>Warning:</strong> This key will only be shown once. Copy it now!
          </div>

          <div className="flex items-center gap-2 rounded bg-muted p-3 font-mono text-sm mb-4">
            <span className="flex-1 break-all select-all">{apiKey.key}</span>
            <Button
              variant="ghost"
              size="sm"
              onClick={handleCopy}
              aria-label="Copy API key to clipboard"
              className="shrink-0"
            >
              {copied ? (
                <Check className="h-4 w-4 text-green-600" aria-hidden />
              ) : (
                <Copy className="h-4 w-4" aria-hidden />
              )}
            </Button>
          </div>

          <label className="flex items-center gap-2 text-sm mb-4">
            <input
              type="checkbox"
              checked={confirmed}
              onChange={(e) => setConfirmed(e.target.checked)}
              className="h-4 w-4 rounded border-input"
            />
            <span>I have copied this key</span>
          </label>

          <Button onClick={onClose} disabled={!confirmed} className="w-full">
            Close
          </Button>
        </div>
      </Card>
    </div>
  );
}

function DeleteApiKeyModal({ apiKey, isDeleting, onConfirm, onCancel }: DeleteApiKeyModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/50" onClick={onCancel} />
      <Card className="relative z-10 w-full max-w-sm" role="alertdialog" aria-modal="true">
        <div className="p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-destructive/10">
              <AlertTriangle className="h-5 w-5 text-destructive" />
            </div>
            <div>
              <h3 className="font-display text-lg font-semibold">Revoke API Key</h3>
              <p className="text-sm text-muted-foreground">This action cannot be undone</p>
            </div>
          </div>
          <p className="mb-4 text-sm">
            Are you sure you want to revoke <span className="font-medium">{apiKey.name}</span>?
            Any applications using this key will immediately lose access.
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onCancel} disabled={isDeleting}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={onConfirm} disabled={isDeleting}>
              {isDeleting ? 'Revoking...' : 'Revoke'}
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
}

function formatRelativeDate(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays === 0) return 'Today';
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;
  if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`;
  return formatDateTime(date);
}

// Relative label with the exact local time on hover
function RelativeDate({ value }: { value: string | null }) {
  return <span title={value ? formatDateTime(value) : undefined}>{formatRelativeDate(value)}</span>;
}

const STAT_WINDOWS = [
  { key: 'last24h', label: '24h', title: 'Emails sent in the last 24 hours' },
  { key: 'last7d', label: '7d', title: 'Emails sent in the last 7 days' },
  { key: 'last30d', label: '30d', title: 'Emails sent in the last 30 days' },
  { key: 'last90d', label: '90d', title: 'Emails sent in the last 90 days' },
] as const;

type StatKey = (typeof STAT_WINDOWS)[number]['key'];
type SortKey = 'name' | 'keyPrefix' | 'access' | 'createdAt' | 'lastUsedAt' | StatKey;
type SortState = { key: SortKey; dir: 'asc' | 'desc' };

const STAT_UNAVAILABLE =
  'Not available: stats are loading or failed, or the window is longer than the sent-email retention.';

// A missing entry with loaded stats means no sends in the last 90 days; null means unknown.
function statValue(stats: ApiKeyStats | undefined, key: StatKey, available: boolean): number | null {
  if (!available) return null;
  return stats ? stats[key] : 0;
}

function accessLabel(apiKey: ApiKey, domains: Domain[]): string {
  if (apiKey.isAdmin) return 'Admin';
  return apiKey.domainIds
    .map((id) => domains.find((d) => d.id === id)?.name ?? '')
    .sort()
    .join(', ');
}

// Missing values (never used, unknown stats) always sort last
function compareNullable<T>(a: T | null, b: T | null, compare: (x: T, y: T) => number, dir: 1 | -1): number {
  if (a === null || b === null) return a === b ? 0 : a === null ? 1 : -1;
  return compare(a, b) * dir;
}

const byText = (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: 'base', numeric: true });
const byNumber = (a: number, b: number) => a - b;

function sortApiKeys(
  keys: ApiKey[],
  sort: SortState,
  domains: Domain[],
  statsByKey: Map<string, ApiKeyStats>,
  statsAvailable: boolean,
): ApiKey[] {
  const dir = sort.dir === 'asc' ? 1 : -1;
  const compare = (a: ApiKey, b: ApiKey): number => {
    switch (sort.key) {
      case 'name':
        return byText(a.name, b.name) * dir;
      case 'keyPrefix':
        return byText(a.keyPrefix, b.keyPrefix) * dir;
      case 'access':
        return byText(accessLabel(a, domains), accessLabel(b, domains)) * dir;
      case 'createdAt':
        return byText(a.createdAt, b.createdAt) * dir; // ISO 8601 UTC sorts as text
      case 'lastUsedAt':
        return compareNullable(a.lastUsedAt, b.lastUsedAt, byText, dir);
      default:
        return compareNullable(
          statValue(statsByKey.get(a.id), sort.key, statsAvailable),
          statValue(statsByKey.get(b.id), sort.key, statsAvailable),
          byNumber,
          dir,
        );
    }
  };
  // Name breaks ties so equal values keep a stable, readable order
  return [...keys].sort((a, b) => compare(a, b) || byText(a.name, b.name));
}

function SortableHeader({
  label,
  sortKey,
  sort,
  onSort,
  title,
  align = 'left',
}: {
  label: string;
  sortKey: SortKey;
  sort?: SortState;
  onSort?: (key: SortKey) => void;
  title?: string;
  align?: 'left' | 'right';
}) {
  const active = sort?.key === sortKey;
  const Icon = !active ? ArrowUpDown : sort.dir === 'asc' ? ArrowUp : ArrowDown;
  return (
    <th
      className={`py-2 px-2 font-medium ${align === 'right' ? 'text-right' : ''}`}
      aria-sort={active ? (sort.dir === 'asc' ? 'ascending' : 'descending') : undefined}
      title={title}
    >
      {onSort ? (
        <button
          type="button"
          onClick={() => onSort(sortKey)}
          className={`inline-flex items-center gap-1 hover:text-foreground ${active ? 'text-foreground' : ''}`}
        >
          {label}
          <Icon className={`h-3 w-3 ${active ? '' : 'opacity-40'}`} />
        </button>
      ) : (
        label
      )}
    </th>
  );
}

function ApiKeyTableHeader({ sort, onSort }: { sort?: SortState; onSort?: (key: SortKey) => void }) {
  return (
    <thead>
      <tr className="border-b text-left text-xs text-muted-foreground">
        <SortableHeader label="Name" sortKey="name" sort={sort} onSort={onSort} />
        <SortableHeader label="Key" sortKey="keyPrefix" sort={sort} onSort={onSort} />
        <SortableHeader label="Access" sortKey="access" sort={sort} onSort={onSort} />
        <SortableHeader label="Created" sortKey="createdAt" sort={sort} onSort={onSort} />
        <SortableHeader label="Last Used" sortKey="lastUsedAt" sort={sort} onSort={onSort} />
        {STAT_WINDOWS.map((w) => (
          <SortableHeader key={w.key} label={`Sent ${w.label}`} sortKey={w.key} sort={sort} onSort={onSort} title={w.title} align="right" />
        ))}
        <th className="py-2 px-2 font-medium"></th>
      </tr>
    </thead>
  );
}

function ApiKeyRow({
  apiKey,
  domains,
  stats,
  statsAvailable,
  onDelete,
  onRename,
}: {
  apiKey: ApiKey;
  domains: Domain[];
  stats?: ApiKeyStats;
  statsAvailable: boolean;
  onDelete: () => void;
  onRename: (name: string) => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(apiKey.name);
  const [saving, setSaving] = useState(false);
  const isRevoked = !!apiKey.revokedAt;

  const startEditing = () => {
    setName(apiKey.name);
    setEditing(true);
  };

  const save = async () => {
    const trimmed = name.trim();
    if (!trimmed || trimmed === apiKey.name) {
      setEditing(false);
      return;
    }
    setSaving(true);
    try {
      await onRename(trimmed);
      setEditing(false);
    } catch {
      // onRename already showed the error; stay in edit mode so the name can be corrected
    } finally {
      setSaving(false);
    }
  };
  const domainNames = apiKey.domainIds
    .map((id) => domains.find((d) => d.id === id)?.name)
    .filter(Boolean);

  return (
    <tr className={`border-b ${isRevoked ? 'opacity-50' : ''}`}>
      <td className="py-3 px-2">
        {editing ? (
          <form
            className="flex items-center gap-1"
            onSubmit={(e) => {
              e.preventDefault();
              void save();
            }}
            onBlur={(e) => {
              // Clicking away cancels; moving focus to Save/Cancel inside the form does not
              if (!saving && !e.currentTarget.contains(e.relatedTarget as Node | null)) setEditing(false);
            }}
          >
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => e.key === 'Escape' && setEditing(false)}
              maxLength={100}
              autoFocus
              disabled={saving}
              aria-label="API key name"
              className="h-8 w-44"
            />
            <Button type="submit" variant="ghost" size="sm" disabled={saving || !name.trim()} aria-label="Save name">
              <Check className="h-3.5 w-3.5" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => setEditing(false)} disabled={saving} aria-label="Cancel rename">
              <X className="h-3.5 w-3.5" />
            </Button>
          </form>
        ) : (
          <span className="inline-flex items-center gap-1">
            <span className={isRevoked ? 'line-through' : ''}>{apiKey.name}</span>
            {!isRevoked && (
              <Button
                variant="ghost"
                size="sm"
                onClick={startEditing}
                aria-label={`Rename ${apiKey.name}`}
                className="h-7 px-1.5 text-muted-foreground hover:text-foreground"
              >
                <Pencil className="h-3 w-3" />
              </Button>
            )}
          </span>
        )}
      </td>
      <td className="py-3 px-2 font-mono text-xs text-muted-foreground">
        {apiKey.keyPrefix}...
      </td>
      <td className="py-3 px-2">
        {isRevoked ? (
          <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs text-muted-foreground">
            Revoked
          </span>
        ) : apiKey.isAdmin ? (
          <span className="inline-flex items-center rounded bg-destructive/10 text-destructive px-2 py-0.5 text-xs font-medium">
            Admin
          </span>
        ) : (
          <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
            {domainNames.length === 0 ? 'No domains' : (
              domainNames.length <= 2
                ? domainNames.join(', ')
                : `${domainNames.slice(0, 2).join(', ')} +${domainNames.length - 2}`
            )}
          </span>
        )}
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        <RelativeDate value={apiKey.createdAt} />
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        <RelativeDate value={apiKey.lastUsedAt} />
      </td>
      {STAT_WINDOWS.map((w) => {
        const value = statValue(stats, w.key, statsAvailable);
        return (
          <td
            key={w.key}
            className="py-3 px-2 text-right font-mono text-xs tabular-nums text-muted-foreground"
            title={value === null ? STAT_UNAVAILABLE : w.title}
          >
            {value ?? '—'}
          </td>
        );
      })}
      <td className="py-3 px-2">
        {!isRevoked && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onDelete}
            className="text-destructive hover:text-destructive hover:bg-destructive/10"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        )}
      </td>
    </tr>
  );
}

function RevokedKeyRow({
  apiKey,
  domains,
}: {
  apiKey: ApiKey;
  domains: Domain[];
}) {
  const domainNames = apiKey.domainIds
    .map((id) => domains.find((d) => d.id === id)?.name)
    .filter(Boolean);

  return (
    <tr className="border-b opacity-60">
      <td className="py-3 px-2">
        <span className="line-through">{apiKey.name}</span>
      </td>
      <td className="py-3 px-2 font-mono text-xs text-muted-foreground">
        {apiKey.keyPrefix}...
      </td>
      <td className="py-3 px-2">
        {apiKey.isAdmin ? (
          <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
            Admin
          </span>
        ) : (
          <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
            {domainNames.length === 0 ? 'No domains' : (
              domainNames.length <= 2
                ? domainNames.join(', ')
                : `${domainNames.slice(0, 2).join(', ')} +${domainNames.length - 2}`
            )}
          </span>
        )}
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        <RelativeDate value={apiKey.revokedAt} />
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        <RelativeDate value={apiKey.lastUsedAt} />
      </td>
    </tr>
  );
}

function ArchivedApiKeyRow({
  archivedKey,
  domains,
}: {
  archivedKey: RevokedApiKey;
  domains: Domain[];
}) {
  const domainNames = archivedKey.domainIds
    .map((id) => domains.find((d) => d.id === id)?.name)
    .filter(Boolean);

  return (
    <tr className="border-b opacity-60">
      <td className="py-3 px-2">
        <span className="line-through">{archivedKey.name}</span>
      </td>
      <td className="py-3 px-2 font-mono text-xs text-muted-foreground">
        {archivedKey.keyPrefix}...
      </td>
      <td className="py-3 px-2">
        {archivedKey.isAdmin ? (
          <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
            Admin
          </span>
        ) : (
          <span className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs">
            {domainNames.length === 0 ? 'No domains' : (
              domainNames.length <= 2
                ? domainNames.join(', ')
                : `${domainNames.slice(0, 2).join(', ')} +${domainNames.length - 2}`
            )}
          </span>
        )}
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        <RelativeDate value={archivedKey.revokedAt} />
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        <RelativeDate value={archivedKey.archivedAt} />
      </td>
    </tr>
  );
}

function EmptyApiKeys({ onCreateClick }: { onCreateClick: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-12 text-center">
      <Key className="h-12 w-12 text-muted-foreground/50 mb-4" />
      <h3 className="text-lg font-medium">No API keys yet</h3>
      <p className="text-sm text-muted-foreground mt-1 max-w-sm">
        Create an API key to start sending emails through the SelfMX API.
      </p>
      <Button className="mt-4" onClick={onCreateClick}>
        <Plus className="h-4 w-4 mr-2" />
        Create API Key
      </Button>
    </div>
  );
}

// API keys are few; load them all so sorting covers every key, not just one page
const ALL_KEYS_LIMIT = 1000;

export function ApiKeysPage() {
  const [sort, setSort] = useState<SortState>({ key: 'name', dir: 'asc' });
  const [archivedPage, setArchivedPage] = useState(1);
  const [showRevokedKeys, setShowRevokedKeys] = useState(false);
  const [showArchivedKeys, setShowArchivedKeys] = useState(false);
  const limit = 20;

  const { data, isLoading, error, refetch } = useApiKeys(1, ALL_KEYS_LIMIT);
  const { data: archivedData, isLoading: archivedLoading } = useRevokedApiKeys(archivedPage, limit);
  const { data: domainsData } = useDomains(1, 100); // Get all domains for selection
  const createMutation = useCreateApiKey();
  const deleteMutation = useDeleteApiKey();
  const renameMutation = useRenameApiKey();
  const { data: statsData, isSuccess: statsAvailable } = useApiKeyStats();
  const statsByKey = useMemo(() => new Map((statsData ?? []).map((s) => [s.apiKeyId, s])), [statsData]);

  // Text columns start A→Z; dates and counts start with the newest/highest
  const handleSort = (key: SortKey) =>
    setSort((current) =>
      current.key === key
        ? { key, dir: current.dir === 'asc' ? 'desc' : 'asc' }
        : { key, dir: ['name', 'keyPrefix', 'access'].includes(key) ? 'asc' : 'desc' },
    );

  const handleRename = async (id: string, name: string) => {
    try {
      await renameMutation.mutateAsync({ id, name });
      toast.success('API key renamed');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to rename API key');
      throw err;
    }
  };

  const [showCreateModal, setShowCreateModal] = useState(false);
  const [createdKey, setCreatedKey] = useState<ApiKeyCreated | null>(null);
  const [keyToDelete, setKeyToDelete] = useState<ApiKey | null>(null);

  const allApiKeys = data?.data ?? [];
  const archivedApiKeys = archivedData?.data ?? [];
  const domains = domainsData?.data ?? [];

  // Split keys into active and revoked (not yet archived)
  const activeKeys = sortApiKeys(allApiKeys.filter(k => !k.revokedAt), sort, domains, statsByKey, statsAvailable);
  const revokedKeys = allApiKeys.filter(k => !!k.revokedAt);

  const totalArchivedKeys = archivedData?.total ?? 0;
  const totalArchivedPages = archivedData ? Math.ceil(archivedData.total / limit) : 0;
  const hasKeys = allApiKeys.length > 0;
  const hasRevokedKeys = revokedKeys.length > 0;
  const hasArchivedKeys = totalArchivedKeys > 0;

  const handleCreate = async (name: string, domainIds: string[], isAdmin: boolean) => {
    try {
      const created = await createMutation.mutateAsync({ name, domainIds, isAdmin });
      setShowCreateModal(false);
      setCreatedKey(created);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to create API key');
    }
  };

  const handleDelete = async () => {
    if (!keyToDelete) return;
    try {
      await deleteMutation.mutateAsync(keyToDelete.id);
      toast.success('API key revoked');
      setKeyToDelete(null);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to revoke API key');
    }
  };

  const handleRevealClose = () => {
    setCreatedKey(null);
  };

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 py-8">
      {isLoading && (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <ApiKeyTableHeader />
              <tbody>
                {[...Array(5)].map((_, i) => (
                  <ApiKeyRowSkeleton key={i} />
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {error && (
        <div className="rounded border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          <span className="font-medium">Error:</span> {error.message}
          <Button variant="link" size="sm" onClick={() => refetch()} className="ml-2">
            Retry
          </Button>
        </div>
      )}

      {!isLoading && !error && !hasKeys && (
        <EmptyApiKeys onCreateClick={() => setShowCreateModal(true)} />
      )}

      {!isLoading && !error && hasKeys && (
        <>
          <div className="mb-4 flex items-center justify-between">
            <div className="text-sm text-muted-foreground">
              {activeKeys.length} active API key{activeKeys.length !== 1 ? 's' : ''}
            </div>
            <Button onClick={() => setShowCreateModal(true)} size="sm">
              <Plus className="h-4 w-4 mr-1.5" />
              Create API Key
            </Button>
          </div>

          <Card>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <ApiKeyTableHeader sort={sort} onSort={handleSort} />
                <tbody>
                  {activeKeys.map((apiKey) => (
                    <ApiKeyRow
                      key={apiKey.id}
                      apiKey={apiKey}
                      domains={domains}
                      stats={statsByKey.get(apiKey.id)}
                      statsAvailable={statsAvailable}
                      onDelete={() => setKeyToDelete(apiKey)}
                      onRename={(name) => handleRename(apiKey.id, name)}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {data && data.total > allApiKeys.length && (
            <p className="mt-3 text-xs text-muted-foreground">
              Showing the first {allApiKeys.length} of {data.total} keys.
            </p>
          )}

          {/* Revoked Keys Section (not yet archived) */}
          {hasRevokedKeys && (
            <div className="mt-8">
              <button
                onClick={() => setShowRevokedKeys(!showRevokedKeys)}
                className="flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
              >
                {showRevokedKeys ? (
                  <ChevronDown className="h-4 w-4" />
                ) : (
                  <ChevronRight className="h-4 w-4" />
                )}
                <Trash2 className="h-4 w-4" />
                <span>Revoked Keys ({revokedKeys.length})</span>
              </button>

              {showRevokedKeys && (
                <div className="mt-3">
                  <p className="text-xs text-muted-foreground mb-3">
                    These keys have been revoked and will be archived after 90 days.
                  </p>

                  <Card>
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b text-left text-xs text-muted-foreground">
                            <th className="py-2 px-2 font-medium">Name</th>
                            <th className="py-2 px-2 font-medium">Key</th>
                            <th className="py-2 px-2 font-medium">Access</th>
                            <th className="py-2 px-2 font-medium">Revoked</th>
                            <th className="py-2 px-2 font-medium">Last Used</th>
                          </tr>
                        </thead>
                        <tbody>
                          {revokedKeys.map((apiKey) => (
                            <RevokedKeyRow
                              key={apiKey.id}
                              apiKey={apiKey}
                              domains={domains}
                            />
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </Card>
                </div>
              )}
            </div>
          )}

          {/* Archived Keys Section (90+ days old) */}
          {hasArchivedKeys && (
            <div className="mt-8">
              <button
                onClick={() => setShowArchivedKeys(!showArchivedKeys)}
                className="flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
              >
                {showArchivedKeys ? (
                  <ChevronDown className="h-4 w-4" />
                ) : (
                  <ChevronRight className="h-4 w-4" />
                )}
                <Archive className="h-4 w-4" />
                <span>Archived Keys ({totalArchivedKeys})</span>
              </button>

              {showArchivedKeys && (
                <div className="mt-3">
                  <p className="text-xs text-muted-foreground mb-3">
                    Keys that were revoked more than 90 days ago are moved here.
                  </p>

                  {archivedLoading ? (
                    <Card>
                      <div className="overflow-x-auto">
                        <table className="w-full text-sm">
                          <thead>
                            <tr className="border-b text-left text-xs text-muted-foreground">
                              <th className="py-2 px-2 font-medium">Name</th>
                              <th className="py-2 px-2 font-medium">Key</th>
                              <th className="py-2 px-2 font-medium">Access</th>
                              <th className="py-2 px-2 font-medium">Revoked</th>
                              <th className="py-2 px-2 font-medium">Archived</th>
                            </tr>
                          </thead>
                          <tbody>
                            {[...Array(3)].map((_, i) => (
                              <ApiKeyRowSkeleton key={i} />
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </Card>
                  ) : (
                    <Card>
                      <div className="overflow-x-auto">
                        <table className="w-full text-sm">
                          <thead>
                            <tr className="border-b text-left text-xs text-muted-foreground">
                              <th className="py-2 px-2 font-medium">Name</th>
                              <th className="py-2 px-2 font-medium">Key</th>
                              <th className="py-2 px-2 font-medium">Access</th>
                              <th className="py-2 px-2 font-medium">Revoked</th>
                              <th className="py-2 px-2 font-medium">Archived</th>
                            </tr>
                          </thead>
                          <tbody>
                            {archivedApiKeys.map((archivedKey) => (
                              <ArchivedApiKeyRow
                                key={archivedKey.id}
                                archivedKey={archivedKey}
                                domains={domains}
                              />
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </Card>
                  )}

                  {totalArchivedPages > 1 && (
                    <div className="mt-4 flex items-center justify-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setArchivedPage((p) => Math.max(1, p - 1))}
                        disabled={archivedPage === 1}
                      >
                        Previous
                      </Button>
                      <span className="px-2 text-sm text-muted-foreground">
                        {archivedPage} / {totalArchivedPages}
                      </span>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setArchivedPage((p) => Math.min(totalArchivedPages, p + 1))}
                        disabled={archivedPage === totalArchivedPages}
                      >
                        Next
                      </Button>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </>
      )}

      {showCreateModal && (
        <CreateApiKeyModal
          domains={domains}
          isCreating={createMutation.isPending}
          onSubmit={handleCreate}
          onCancel={() => setShowCreateModal(false)}
        />
      )}

      {createdKey && (
        <ApiKeyRevealModal apiKey={createdKey} onClose={handleRevealClose} />
      )}

      {keyToDelete && (
        <DeleteApiKeyModal
          apiKey={keyToDelete}
          isDeleting={deleteMutation.isPending}
          onConfirm={handleDelete}
          onCancel={() => setKeyToDelete(null)}
        />
      )}
    </div>
  );
}
