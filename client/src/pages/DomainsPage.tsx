import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { toast } from 'sonner';
import { useDomains, useCreateDomain, useDomainStats } from '@/hooks/useDomains';
import { DomainStatusBadge } from '@/components/DomainStatusBadge';
import { SortableHeader } from '@/components/SortableHeader';
import { Sparkline } from '@/components/Sparkline';
import { formatDateTime } from '@/lib/date';
import { byNumber, byText, compareNullable, nextSort, type SortState } from '@/lib/sort';
import type { Domain, DomainStats } from '@/lib/schemas';
import { STAT_UNAVAILABLE, STAT_WINDOWS, statValue, type StatKey } from '@/lib/sendStats';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

type DomainSortKey = 'name' | 'status' | 'createdAt' | 'verifiedAt' | 'lastCheckedAt' | 'activeApiKeys' | StatKey;

type StatsLookup = { byDomain: Map<string, DomainStats>; available: boolean };

// Active keys: missing entry with loaded stats = none; unknown while loading
function activeKeysValue(stats: DomainStats | undefined, available: boolean): number | null {
  return available ? (stats?.activeApiKeys ?? 0) : null;
}

// The list endpoint returns at most 100 per page; load them all so sorting covers every domain
const ALL_DOMAINS_LIMIT = 100;

function sortDomains(domains: Domain[], sort: SortState<DomainSortKey>, stats: StatsLookup): Domain[] {
  const dir = sort.dir === 'asc' ? 1 : -1;
  const compare = (a: Domain, b: Domain): number => {
    switch (sort.key) {
      case 'name':
        return byText(a.name, b.name) * dir;
      case 'status':
        return byText(a.status, b.status) * dir;
      case 'createdAt':
        return byText(a.createdAt, b.createdAt) * dir; // ISO 8601 UTC sorts as text
      case 'verifiedAt':
      case 'lastCheckedAt':
        return compareNullable(a[sort.key], b[sort.key], byText, dir);
      case 'activeApiKeys':
        return compareNullable(
          activeKeysValue(stats.byDomain.get(a.id), stats.available),
          activeKeysValue(stats.byDomain.get(b.id), stats.available),
          byNumber,
          dir,
        );
      default:
        return compareNullable(
          statValue(stats.byDomain.get(a.id), sort.key, stats.available),
          statValue(stats.byDomain.get(b.id), sort.key, stats.available),
          byNumber,
          dir,
        );
    }
  };
  return [...domains].sort((a, b) => compare(a, b) || byText(a.name, b.name));
}

function DomainTableHeader({
  sort,
  onSort,
}: {
  sort?: SortState<DomainSortKey>;
  onSort?: (key: DomainSortKey) => void;
}) {
  return (
    <thead>
      <tr className="border-b text-left text-xs text-muted-foreground">
        <SortableHeader label="Domain" sortKey="name" sort={sort} onSort={onSort} />
        <SortableHeader label="Status" sortKey="status" sort={sort} onSort={onSort} />
        <SortableHeader label="Added" sortKey="createdAt" sort={sort} onSort={onSort} />
        <SortableHeader label="Verified" sortKey="verifiedAt" sort={sort} onSort={onSort} />
        <SortableHeader label="Last checked" sortKey="lastCheckedAt" sort={sort} onSort={onSort} />
        <SortableHeader
          label="Active keys"
          sortKey="activeApiKeys"
          sort={sort}
          onSort={onSort}
          title="Non-revoked API keys scoped to this domain (admin keys can use every domain and are not counted)"
          align="right"
        />
        <th className="py-2 px-2 font-medium" title="Emails sent per day over the last 30 days (UTC days)">Last 30 days</th>
        {STAT_WINDOWS.map((w) => (
          <SortableHeader key={w.key} label={`Sent ${w.label}`} sortKey={w.key} sort={sort} onSort={onSort} title={w.title} align="right" />
        ))}
        <th className="py-2 px-2 font-medium">Notes</th>
        <th className="py-2 px-2 font-medium"></th>
      </tr>
    </thead>
  );
}

const numberCell = 'py-3 px-2 text-right font-mono text-xs tabular-nums text-muted-foreground';

function DomainRow({ domain, stats, statsAvailable }: { domain: Domain; stats?: DomainStats; statsAvailable: boolean }) {
  const activeKeys = activeKeysValue(stats, statsAvailable);
  const note =
    domain.failureReason ?? (domain.status === 'verifying' ? 'Verifying DNS records...' : null);
  return (
    <tr className="border-b last:border-0">
      <td className="py-3 px-2">
        <Link to={`/domains/${domain.id}`} className="font-mono text-sm font-medium hover:text-primary transition-colors">
          {domain.name}
        </Link>
      </td>
      <td className="py-3 px-2">
        <DomainStatusBadge status={domain.status} />
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground whitespace-nowrap">{formatDateTime(domain.createdAt)}</td>
      <td className="py-3 px-2 text-xs text-muted-foreground whitespace-nowrap">
        {domain.verifiedAt ? formatDateTime(domain.verifiedAt) : '—'}
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground whitespace-nowrap">
        {domain.lastCheckedAt ? formatDateTime(domain.lastCheckedAt) : '—'}
      </td>
      <td className={numberCell} title={activeKeys === null ? STAT_UNAVAILABLE : 'Non-revoked API keys scoped to this domain'}>
        {activeKeys ?? '—'}
      </td>
      <td className="py-3 px-2">
        <Sparkline values={statsAvailable ? (stats ? stats.daily30d : new Array(30).fill(0)) : null} />
      </td>
      {STAT_WINDOWS.map((w) => {
        const value = statValue(stats, w.key, statsAvailable);
        return (
          <td key={w.key} className={numberCell} title={value === null ? STAT_UNAVAILABLE : w.title}>
            {value ?? '—'}
          </td>
        );
      })}
      <td
        className={`py-3 px-2 text-xs max-w-md truncate ${domain.failureReason ? 'text-[var(--status-failed-text)]' : 'text-muted-foreground'}`}
        title={note ?? undefined}
      >
        {note ?? ''}
      </td>
      <td className="py-3 px-2 text-right">
        <Link
          to={`/domains/${domain.id}`}
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
        >
          Details
          <ChevronRight className="h-3 w-3" />
        </Link>
      </td>
    </tr>
  );
}

function DomainRowSkeleton() {
  return (
    <tr className="border-b">
      <td className="py-3 px-2"><Skeleton className="h-4 w-40" /></td>
      <td className="py-3 px-2"><Skeleton className="h-5 w-16 rounded" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-32" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-32" /></td>
      <td className="py-3 px-2"><Skeleton className="h-4 w-32" /></td>
      <td className="py-3 px-2"><Skeleton className="ml-auto h-4 w-8" /></td>
      <td className="py-3 px-2"><Skeleton className="h-5 w-[119px]" /></td>
      {STAT_WINDOWS.map((w) => (
        <td key={w.key} className="py-3 px-2"><Skeleton className="ml-auto h-4 w-8" /></td>
      ))}
      <td className="py-3 px-2"><Skeleton className="h-4 w-24" /></td>
      <td className="py-3 px-2"><Skeleton className="ml-auto h-4 w-12" /></td>
    </tr>
  );
}

export function DomainsPage() {
  const navigate = useNavigate();
  const [newDomain, setNewDomain] = useState('');
  const [sort, setSort] = useState<SortState<DomainSortKey>>({ key: 'name', dir: 'asc' });
  const [showAddModal, setShowAddModal] = useState(false);
  const [showErrorModal, setShowErrorModal] = useState(false);
  const { data, isLoading, error } = useDomains(1, ALL_DOMAINS_LIMIT);
  const { data: statsData, isSuccess: statsAvailable } = useDomainStats();
  const stats = useMemo<StatsLookup>(
    () => ({ byDomain: new Map((statsData ?? []).map((s) => [s.domainId, s])), available: statsAvailable }),
    [statsData, statsAvailable],
  );
  const createMutation = useCreateDomain();
  const domains = data?.data ?? [];
  const totalDomains = data?.total ?? 0;
  const hasDomains = domains.length > 0;
  const showInitialError = !!error && !data && !isLoading;

  useEffect(() => {
    if (showInitialError) {
      setShowErrorModal(true);
    }
  }, [showInitialError]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newDomain.trim()) return;

    try {
      const created = await createMutation.mutateAsync(newDomain.trim());
      toast.success(`Domain ${newDomain.trim()} added`);
      setNewDomain('');
      navigate(`/domains/${created.id}`);
      return true;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to add domain');
      return false;
    }
  };

  const handleSort = (key: DomainSortKey) => setSort((current) => nextSort(current, key, ['name', 'status']));

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 py-8">
      {!hasDomains && !isLoading && (
        <section className="mx-auto max-w-md">
          <div className="mb-6">
            <h2 className="font-display text-xl font-semibold">
              Add your first domain
            </h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Enter a domain to set up AWS SES verification. We'll provide DNS records for MX, SPF, DKIM, and DMARC.
            </p>
          </div>
          <form onSubmit={handleCreate} className="flex gap-2">
            <Input
              placeholder="example.com"
              value={newDomain}
              onChange={(e) => setNewDomain(e.target.value)}
              className="font-mono"
            />
            <Button
              type="submit"
              disabled={createMutation.isPending || !newDomain.trim()}
            >
              {createMutation.isPending ? 'Adding...' : 'Add'}
            </Button>
          </form>
        </section>
      )}

      {isLoading && (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <DomainTableHeader />
              <tbody>
                {[...Array(4)].map((_, i) => (
                  <DomainRowSkeleton key={i} />
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {error && !showInitialError && (
        <div className="rounded border border-[var(--status-failed-text)]/20 bg-[var(--status-failed-bg)] px-4 py-3 text-sm text-[var(--status-failed-text)]">
          <span className="font-medium">Error:</span> {error.message}
        </div>
      )}

      {data && hasDomains && (
        <>
          <div className="mb-4 flex items-center justify-between">
            <div className="text-sm text-muted-foreground">
              {totalDomains} domain{totalDomains !== 1 ? 's' : ''}
            </div>
            <Button onClick={() => setShowAddModal(true)} size="sm">
              Add domain
            </Button>
          </div>

          <Card>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <DomainTableHeader sort={sort} onSort={handleSort} />
                <tbody>
                  {sortDomains(domains, sort, stats).map((domain) => (
                    <DomainRow
                      key={domain.id}
                      domain={domain}
                      stats={stats.byDomain.get(domain.id)}
                      statsAvailable={stats.available}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {totalDomains > domains.length && (
            <p className="mt-3 text-xs text-muted-foreground">
              Showing the first {domains.length} of {totalDomains} domains.
            </p>
          )}
        </>
      )}

      {(showAddModal || (showInitialError && showErrorModal)) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div
            className="absolute inset-0 bg-black/50"
            onClick={() => {
              if (showAddModal) setShowAddModal(false);
              if (showErrorModal) setShowErrorModal(false);
            }}
          />
          {showAddModal && (
            <Card className="relative z-10 w-full max-w-sm" role="dialog" aria-modal="true">
              <div className="p-5">
                <div className="mb-4 flex items-center justify-between">
                  <h3 className="font-display text-lg font-semibold">Add domain</h3>
                  <Button variant="ghost" size="sm" onClick={() => setShowAddModal(false)}>
                    Close
                  </Button>
                </div>
                <form
                  onSubmit={async (e) => {
                    const created = await handleCreate(e);
                    if (created) setShowAddModal(false);
                  }}
                  className="flex gap-2"
                >
                  <Input
                    placeholder="example.com"
                    value={newDomain}
                    onChange={(e) => setNewDomain(e.target.value)}
                    className="font-mono"
                    autoFocus
                  />
                  <Button
                    type="submit"
                    disabled={createMutation.isPending || !newDomain.trim()}
                  >
                    {createMutation.isPending ? 'Adding...' : 'Add'}
                  </Button>
                </form>
              </div>
            </Card>
          )}

          {showInitialError && showErrorModal && (
            <Card className="relative z-10 w-full max-w-sm" role="alertdialog" aria-modal="true">
              <div className="p-5">
                <div className="mb-4 flex items-center justify-between">
                  <h3 className="font-display text-lg font-semibold text-destructive">
                    Error
                  </h3>
                  <Button variant="ghost" size="sm" onClick={() => setShowErrorModal(false)}>
                    Close
                  </Button>
                </div>
                <p className="text-sm text-muted-foreground">{error.message}</p>
              </div>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}
