import { useState } from 'react';
import { toast } from 'sonner';
import { Key, Plus, Trash2, AlertTriangle, Copy, Check, ChevronDown, ChevronRight, Archive } from 'lucide-react';
import { useApiKeys, useCreateApiKey, useDeleteApiKey, useRevokedApiKeys } from '@/hooks/useApiKeys';
import { useDomains } from '@/hooks/useDomains';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import type { ApiKey, ApiKeyCreated, Domain, RevokedApiKey } from '@/lib/schemas';

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
  return date.toLocaleDateString();
}

function ApiKeyRow({
  apiKey,
  domains,
  onDelete,
}: {
  apiKey: ApiKey;
  domains: Domain[];
  onDelete: () => void;
}) {
  const isRevoked = !!apiKey.revokedAt;
  const domainNames = apiKey.domainIds
    .map((id) => domains.find((d) => d.id === id)?.name)
    .filter(Boolean);

  return (
    <tr className={`border-b ${isRevoked ? 'opacity-50' : ''}`}>
      <td className="py-3 px-2">
        <span className={isRevoked ? 'line-through' : ''}>{apiKey.name}</span>
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
        {formatRelativeDate(apiKey.createdAt)}
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        {formatRelativeDate(apiKey.lastUsedAt)}
      </td>
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

function RevokedApiKeyRow({
  revokedKey,
  domains,
}: {
  revokedKey: RevokedApiKey;
  domains: Domain[];
}) {
  const domainNames = revokedKey.domainIds
    .map((id) => domains.find((d) => d.id === id)?.name)
    .filter(Boolean);

  return (
    <tr className="border-b opacity-60">
      <td className="py-3 px-2">
        <span className="line-through">{revokedKey.name}</span>
      </td>
      <td className="py-3 px-2 font-mono text-xs text-muted-foreground">
        {revokedKey.keyPrefix}...
      </td>
      <td className="py-3 px-2">
        {revokedKey.isAdmin ? (
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
        {formatRelativeDate(revokedKey.revokedAt)}
      </td>
      <td className="py-3 px-2 text-xs text-muted-foreground">
        {formatRelativeDate(revokedKey.archivedAt)}
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

export function ApiKeysPage() {
  const [page, setPage] = useState(1);
  const [revokedPage, setRevokedPage] = useState(1);
  const [showRevokedKeys, setShowRevokedKeys] = useState(false);
  const limit = 20;

  const { data, isLoading, error, refetch } = useApiKeys(page, limit);
  const { data: revokedData, isLoading: revokedLoading } = useRevokedApiKeys(revokedPage, limit);
  const { data: domainsData } = useDomains(1, 100); // Get all domains for selection
  const createMutation = useCreateApiKey();
  const deleteMutation = useDeleteApiKey();

  const [showCreateModal, setShowCreateModal] = useState(false);
  const [createdKey, setCreatedKey] = useState<ApiKeyCreated | null>(null);
  const [keyToDelete, setKeyToDelete] = useState<ApiKey | null>(null);

  const apiKeys = data?.data ?? [];
  const revokedApiKeys = revokedData?.data ?? [];
  const domains = domainsData?.data ?? [];
  const totalKeys = data?.total ?? 0;
  const totalRevokedKeys = revokedData?.total ?? 0;
  const totalPages = data ? Math.ceil(data.total / limit) : 0;
  const totalRevokedPages = revokedData ? Math.ceil(revokedData.total / limit) : 0;
  const hasKeys = apiKeys.length > 0;
  const hasRevokedKeys = totalRevokedKeys > 0;

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
    <div className="container mx-auto max-w-4xl px-4 py-8">
      {isLoading && (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs text-muted-foreground">
                  <th className="py-2 px-2 font-medium">Name</th>
                  <th className="py-2 px-2 font-medium">Key</th>
                  <th className="py-2 px-2 font-medium">Access</th>
                  <th className="py-2 px-2 font-medium">Created</th>
                  <th className="py-2 px-2 font-medium">Last Used</th>
                  <th className="py-2 px-2 font-medium"></th>
                </tr>
              </thead>
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
              {totalKeys} API key{totalKeys !== 1 ? 's' : ''}
            </div>
            <Button onClick={() => setShowCreateModal(true)} size="sm">
              <Plus className="h-4 w-4 mr-1.5" />
              Create API Key
            </Button>
          </div>

          <Card>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-xs text-muted-foreground">
                    <th className="py-2 px-2 font-medium">Name</th>
                    <th className="py-2 px-2 font-medium">Key</th>
                    <th className="py-2 px-2 font-medium">Access</th>
                    <th className="py-2 px-2 font-medium">Created</th>
                    <th className="py-2 px-2 font-medium">Last Used</th>
                    <th className="py-2 px-2 font-medium"></th>
                  </tr>
                </thead>
                <tbody>
                  {apiKeys.map((apiKey) => (
                    <ApiKeyRow
                      key={apiKey.id}
                      apiKey={apiKey}
                      domains={domains}
                      onDelete={() => setKeyToDelete(apiKey)}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {totalPages > 1 && (
            <div className="mt-6 flex items-center justify-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                Previous
              </Button>
              <span className="px-2 text-sm text-muted-foreground">
                {page} / {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
              >
                Next
              </Button>
            </div>
          )}

          {/* Revoked Keys Section */}
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
                <Archive className="h-4 w-4" />
                <span>Archived Keys ({totalRevokedKeys})</span>
              </button>

              {showRevokedKeys && (
                <div className="mt-3">
                  <p className="text-xs text-muted-foreground mb-3">
                    Keys that were revoked more than 90 days ago are moved here.
                  </p>

                  {revokedLoading ? (
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
                            {revokedApiKeys.map((revokedKey) => (
                              <RevokedApiKeyRow
                                key={revokedKey.id}
                                revokedKey={revokedKey}
                                domains={domains}
                              />
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </Card>
                  )}

                  {totalRevokedPages > 1 && (
                    <div className="mt-4 flex items-center justify-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setRevokedPage((p) => Math.max(1, p - 1))}
                        disabled={revokedPage === 1}
                      >
                        Previous
                      </Button>
                      <span className="px-2 text-sm text-muted-foreground">
                        {revokedPage} / {totalRevokedPages}
                      </span>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setRevokedPage((p) => Math.min(totalRevokedPages, p + 1))}
                        disabled={revokedPage === totalRevokedPages}
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
