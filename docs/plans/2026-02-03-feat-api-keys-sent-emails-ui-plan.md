---
title: "feat: Add API Keys management and Sent Emails log to admin UI"
type: feat
date: 2026-02-03
deepened: 2026-02-03
---

# Add API Keys Management and Sent Emails Log to Admin UI

## Enhancement Summary

**Deepened on:** 2026-02-03
**Sections enhanced:** 5
**Research agents used:** Frontend Design, React Query Patterns, Zod Schema Patterns, API Key UX, Email Preview Security, Date Range Filter UX

### Key Improvements
1. **Enhanced security for HTML email rendering** - DOMPurify sanitization + sandboxed iframe + CSP
2. **Improved cursor pagination** - useInfiniteQuery pattern with proper cache invalidation
3. **Better API key UX** - Single-reveal pattern following Stripe/GitHub conventions
4. **Accessibility improvements** - ARIA labels, keyboard navigation, focus management
5. **URL state synchronization** - Shareable filter URLs with useSearchParams

### New Considerations Discovered
- Use DOMPurify to sanitize HTML before rendering in iframe (defense in depth)
- Implement copy-to-clipboard with both Clipboard API and fallback
- Date filters should sync to URL for shareable links
- Empty states should guide users toward next action
- Revoked keys should be visually distinct but still visible for audit

---

## Overview

The SelfMX admin UI currently only supports domain management. Users cannot create or manage API keys through the UI, nor can they view a history of sent emails. Both features have working backend endpoints but no frontend implementation.

This plan adds two new admin pages:
1. **API Keys Page** - List, create, and revoke API keys with domain scoping
2. **Sent Emails Page** - View sent email history with filtering and detail view

## Problem Statement

Currently, API keys can only be managed via direct API calls or database access. This makes SelfMX difficult to use for non-technical users and prevents visibility into email sending activity. Admins need:

1. A way to create API keys scoped to specific domains
2. A way to revoke compromised or unused keys
3. Visibility into what emails have been sent, when, and to whom

## Proposed Solution

Add two new pages accessible from the header navigation, following existing patterns from `DomainsPage` and `DomainDetailPage`.

### Navigation

Add header links next to the existing "Jobs" link:

```
SelfMX | Email API                    API Keys | Sent Emails | Jobs | [Theme] | [Logout]
```

Routes:
- `/api-keys` - API Keys list page
- `/api-keys/:id` - API Key detail page (optional, could be inline modal)
- `/sent-emails` - Sent emails list page
- `/sent-emails/:id` - Sent email detail page

## Technical Approach

### Phase 1: API Client & Schemas

Add Zod schemas and API client methods for new endpoints.

#### client/src/lib/schemas.ts

```typescript
// API Key schemas
export const ApiKeySchema = z.object({
  id: z.string(),
  name: z.string(),
  keyPrefix: z.string(),
  isAdmin: z.boolean(),
  createdAt: z.string().datetime(),
  revokedAt: z.string().datetime().nullable(),
  lastUsedAt: z.string().datetime().nullable(),
  domainIds: z.array(z.string()),
});
export type ApiKey = z.infer<typeof ApiKeySchema>;

export const ApiKeyCreatedSchema = z.object({
  id: z.string(),
  name: z.string(),
  key: z.string(), // Full key, shown only once
  createdAt: z.string().datetime(),
});
export type ApiKeyCreated = z.infer<typeof ApiKeyCreatedSchema>;

export const PaginatedApiKeysSchema = z.object({
  data: z.array(ApiKeySchema),
  page: z.number(),
  limit: z.number(),
  total: z.number(),
});
export type PaginatedApiKeys = z.infer<typeof PaginatedApiKeysSchema>;

// Sent Email schemas
export const SentEmailListItemSchema = z.object({
  id: z.string(),
  messageId: z.string(),
  sentAt: z.string().datetime(),
  fromAddress: z.string(),
  to: z.array(z.string()),
  subject: z.string(),
  domainId: z.string(),
});
export type SentEmailListItem = z.infer<typeof SentEmailListItemSchema>;

export const SentEmailDetailSchema = z.object({
  id: z.string(),
  messageId: z.string(),
  sentAt: z.string().datetime(),
  fromAddress: z.string(),
  to: z.array(z.string()),
  cc: z.array(z.string()).nullable(),
  replyTo: z.string().nullable(),
  subject: z.string(),
  htmlBody: z.string().nullable(),
  textBody: z.string().nullable(),
  domainId: z.string(),
});
export type SentEmailDetail = z.infer<typeof SentEmailDetailSchema>;

export const CursorPagedSentEmailsSchema = z.object({
  data: z.array(SentEmailListItemSchema),
  nextCursor: z.string().nullable(),
  hasMore: z.boolean(),
});
export type CursorPagedSentEmails = z.infer<typeof CursorPagedSentEmailsSchema>;
```

#### Research Insights: Zod Schema Patterns

**Best Practices:**
- Use `.nullable()` for optional fields that may be null from API, vs `.optional()` for fields that may be omitted entirely
- Use schema composition with `.extend()` for shared base schemas (SentEmailListItem → SentEmailDetail)
- Export both schema and inferred type together for type safety

**Edge Cases:**
- Handle datetime parsing failures gracefully - backend returns ISO 8601 strings
- Use `.default([])` for arrays that should never be undefined in components
- Consider `.transform()` for date parsing if working with Date objects

**Implementation Detail:**
```typescript
// Compose schemas for DRY code
const SentEmailBaseSchema = z.object({
  id: z.string(),
  messageId: z.string(),
  sentAt: z.string().datetime(),
  fromAddress: z.string(),
  to: z.array(z.string()),
  subject: z.string(),
  domainId: z.string(),
});

export const SentEmailListItemSchema = SentEmailBaseSchema;
export const SentEmailDetailSchema = SentEmailBaseSchema.extend({
  cc: z.array(z.string()).nullable(),
  replyTo: z.string().nullable(),
  htmlBody: z.string().nullable(),
  textBody: z.string().nullable(),
});
```

#### client/src/lib/api.ts

Add methods:
- [x] `listApiKeys(page, limit)` - GET /v1/api-keys
- [x] `getApiKey(id)` - GET /v1/api-keys/{id}
- [x] `createApiKey(name, domainIds, isAdmin)` - POST /v1/api-keys
- [x] `deleteApiKey(id)` - DELETE /v1/api-keys/{id}
- [x] `listSentEmails(params)` - GET /v1/sent-emails with cursor, filters
- [x] `getSentEmail(id)` - GET /v1/sent-emails/{id}

### Phase 2: React Query Hooks

#### client/src/hooks/useApiKeys.ts

```typescript
export const apiKeyKeys = {
  all: ['apiKeys'] as const,
  lists: () => [...apiKeyKeys.all, 'list'] as const,
  list: (page: number, limit: number) => [...apiKeyKeys.lists(), { page, limit }] as const,
  details: () => [...apiKeyKeys.all, 'detail'] as const,
  detail: (id: string) => [...apiKeyKeys.details(), id] as const,
};

export function useApiKeys(page = 1, limit = 20) { ... }
export function useApiKey(id: string) { ... }
export function useCreateApiKey() { ... }
export function useDeleteApiKey() { ... }
```

#### client/src/hooks/useSentEmails.ts

```typescript
export const sentEmailKeys = {
  all: ['sentEmails'] as const,
  lists: () => [...sentEmailKeys.all, 'list'] as const,
  list: (filters: SentEmailFilters) => [...sentEmailKeys.lists(), filters] as const,
  details: () => [...sentEmailKeys.all, 'detail'] as const,
  detail: (id: string) => [...sentEmailKeys.details(), id] as const,
};

export function useSentEmails(filters: SentEmailFilters) { ... }
export function useSentEmail(id: string) { ... }
```

#### Research Insights: React Query Patterns

**Best Practices (TanStack Query v5):**
- Use `useInfiniteQuery` for cursor pagination instead of manual state management
- Configure `staleTime` based on data freshness needs (API keys rarely change: 5min, emails: 1min)
- Use `queryClient.invalidateQueries()` after mutations for proper cache sync
- Prefer `select` option for data transformation to avoid re-renders

**Cursor Pagination with useInfiniteQuery:**
```typescript
export function useSentEmails(filters: SentEmailFilters) {
  return useInfiniteQuery({
    queryKey: sentEmailKeys.list(filters),
    queryFn: ({ pageParam }) => api.listSentEmails({ ...filters, cursor: pageParam }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.hasMore ? lastPage.nextCursor : undefined,
    staleTime: 60 * 1000, // 1 minute
  });
}

// Usage in component:
const { data, fetchNextPage, hasNextPage, isFetchingNextPage } = useSentEmails(filters);
const allEmails = data?.pages.flatMap(page => page.data) ?? [];
```

**Mutation with Cache Invalidation:**
```typescript
export function useCreateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: CreateApiKeyParams) => api.createApiKey(params),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: apiKeyKeys.lists() });
    },
  });
}

export function useDeleteApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteApiKey(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: apiKeyKeys.lists() });
      queryClient.removeQueries({ queryKey: apiKeyKeys.detail(id) });
    },
  });
}
```

**Edge Cases:**
- Handle filter changes resetting pagination (filters in queryKey handles this)
- Use `enabled: !!id` to prevent queries with undefined IDs
- Consider `placeholderData` for instant perceived loading on filter changes

### Phase 3: API Keys Page

#### client/src/pages/ApiKeysPage.tsx

Components:
- [x] `ApiKeysPage` - Main page with list and create button
- [x] `ApiKeyRow` - Table row showing key info
- [x] `CreateApiKeyModal` - Modal for creating new key
- [x] `ApiKeyRevealModal` - Modal showing newly created key (CRITICAL: shown only once)
- [x] `DeleteApiKeyModal` - Confirmation for revocation

Key states:
- Empty state: "No API keys yet. Create one to start sending emails."
- Loading: Skeleton rows
- Error: Error message with retry button

#### Research Insights: Empty States & Loading

**Empty State Design:**
```tsx
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
```

**Skeleton Loading Pattern:**
```tsx
function ApiKeyRowSkeleton() {
  return (
    <TableRow>
      <TableCell><Skeleton className="h-4 w-32" /></TableCell>
      <TableCell><Skeleton className="h-4 w-24 font-mono" /></TableCell>
      <TableCell><Skeleton className="h-5 w-16" /></TableCell>
      <TableCell><Skeleton className="h-4 w-20" /></TableCell>
      <TableCell><Skeleton className="h-4 w-20" /></TableCell>
      <TableCell><Skeleton className="h-8 w-16" /></TableCell>
    </TableRow>
  );
}

// Show 3-5 skeleton rows while loading
{isLoading && Array(5).fill(0).map((_, i) => <ApiKeyRowSkeleton key={i} />)}
```

**Error State with Retry:**
```tsx
function ErrorState({ error, onRetry }: { error: Error; onRetry: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-12 text-center">
      <AlertCircle className="h-12 w-12 text-destructive/50 mb-4" />
      <h3 className="text-lg font-medium">Failed to load</h3>
      <p className="text-sm text-muted-foreground mt-1">
        {error.message || 'Something went wrong'}
      </p>
      <Button variant="outline" className="mt-4" onClick={onRetry}>
        <RefreshCw className="h-4 w-4 mr-2" />
        Try Again
      </Button>
    </div>
  );
}
```

#### Create API Key Flow

```
1. User clicks "Create API Key"
2. Modal opens with:
   - Name input (required)
   - "Admin key" checkbox (grants access to all domains)
   - Domain multi-select (disabled if admin, required if not admin)
3. User fills form and clicks "Create"
4. POST /v1/api-keys
5. On success: Show ApiKeyRevealModal with:
   - Full API key displayed prominently
   - Copy button with "Copied!" feedback
   - Warning: "This key will only be shown once. Copy it now!"
   - "I have copied this key" checkbox (required to close)
   - Close button (disabled until checkbox checked)
6. User copies key and confirms
7. Modal closes, list refreshes
```

#### API Key List Columns

| Column | Content |
|--------|---------|
| Name | Key name |
| Key | Prefix only: `re_abc123...` |
| Type | "Admin" badge or domain count |
| Created | Relative date |
| Last Used | Relative date or "Never" |
| Actions | Revoke button |

#### Research Insights: API Key UX Patterns

**Best Practices (following Stripe/GitHub conventions):**
- Show key prefix with ellipsis (`re_abc123...`) - never the full key
- Use monospace font for key display for visual clarity
- Single-reveal pattern: key shown ONCE in a prominent modal after creation
- Copy button with visual feedback ("Copied!" for 2 seconds, then reset)
- Require explicit acknowledgment before closing reveal modal

**Copy-to-Clipboard with Fallback:**
```typescript
async function copyToClipboard(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    // Fallback for older browsers or restricted contexts
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    const success = document.execCommand('copy');
    document.body.removeChild(textarea);
    return success;
  }
}
```

**Key Reveal Modal Pattern:**
```tsx
<Dialog open={!!createdKey} onOpenChange={() => {}}>
  <DialogContent onInteractOutside={(e) => e.preventDefault()}>
    <DialogHeader>
      <DialogTitle>API Key Created</DialogTitle>
    </DialogHeader>

    <Alert variant="warning">
      <AlertCircle className="h-4 w-4" />
      <AlertDescription>
        This key will only be shown once. Copy it now!
      </AlertDescription>
    </Alert>

    <div className="flex items-center gap-2 p-3 bg-muted rounded-md font-mono text-sm">
      <span className="flex-1 break-all">{createdKey}</span>
      <Button size="sm" variant="ghost" onClick={handleCopy}>
        {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
      </Button>
    </div>

    <div className="flex items-center gap-2">
      <Checkbox id="confirm" checked={confirmed} onCheckedChange={setConfirmed} />
      <Label htmlFor="confirm">I have copied this key</Label>
    </div>

    <Button onClick={handleClose} disabled={!confirmed}>
      Close
    </Button>
  </DialogContent>
</Dialog>
```

**Visual Badges:**
- Admin keys: `<Badge variant="destructive">Admin</Badge>`
- Revoked keys: Strike-through text + muted colors + "Revoked" badge
- Domain count: `<Badge variant="secondary">3 domains</Badge>`

**Accessibility:**
- Copy button: `aria-label="Copy API key to clipboard"`
- Success feedback: Use `aria-live="polite"` for "Copied!" announcement
- Focus trap in reveal modal until confirmed

### Phase 4: Sent Emails Page

#### client/src/pages/SentEmailsPage.tsx

Components:
- [x] `SentEmailsPage` - Main page with list and filters
- [x] `SentEmailRow` - Table row showing email summary
- [x] `SentEmailFilters` - Domain dropdown + date range
- [x] `SentEmailDetailModal` - Modal showing full email details

#### Filters

- Domain dropdown: Populated from `/v1/domains` (reuse `useDomains` query)
- Date range: Simple date inputs with presets (Today, Last 7 days, Last 30 days)
- Clear filters button

#### Research Insights: Date Range Filter UX

**Best Practices:**
- Sync filter state to URL params using `useSearchParams` for shareable links
- Provide preset buttons (Today, 7 days, 30 days) alongside custom date inputs
- Use ISO 8601 format for URL params: `?from=2026-02-01&to=2026-02-03`
- Clear button should reset both URL and state

**URL State Synchronization:**
```typescript
interface SentEmailFilters {
  domainId?: string;
  from?: string;  // ISO date string
  to?: string;    // ISO date string
}

function useSentEmailFilters() {
  const [searchParams, setSearchParams] = useSearchParams();

  const filters: SentEmailFilters = {
    domainId: searchParams.get('domainId') ?? undefined,
    from: searchParams.get('from') ?? undefined,
    to: searchParams.get('to') ?? undefined,
  };

  const setFilters = (newFilters: Partial<SentEmailFilters>) => {
    const params = new URLSearchParams(searchParams);
    Object.entries(newFilters).forEach(([key, value]) => {
      if (value) params.set(key, value);
      else params.delete(key);
    });
    setSearchParams(params, { replace: true });
  };

  const clearFilters = () => setSearchParams({}, { replace: true });

  return { filters, setFilters, clearFilters };
}
```

**Date Preset Component:**
```tsx
const presets = [
  { label: 'Today', days: 0 },
  { label: 'Last 7 days', days: 7 },
  { label: 'Last 30 days', days: 30 },
];

function DatePresets({ onSelect }: { onSelect: (from: string, to: string) => void }) {
  return (
    <div className="flex gap-1">
      {presets.map(({ label, days }) => (
        <Button
          key={label}
          variant="outline"
          size="sm"
          onClick={() => {
            const to = new Date().toISOString().split('T')[0];
            const from = new Date(Date.now() - days * 86400000).toISOString().split('T')[0];
            onSelect(from, to);
          }}
        >
          {label}
        </Button>
      ))}
    </div>
  );
}
```

**Accessibility:**
- Date inputs: Use `<input type="date">` for native date picker
- Preset buttons: Group with `role="group"` and `aria-label="Date presets"`
- Clear button: `aria-label="Clear all filters"`

#### Cursor Pagination

Use "Load More" pattern (not page numbers):
```tsx
{hasMore && (
  <Button onClick={loadMore} disabled={isLoadingMore}>
    {isLoadingMore ? 'Loading...' : 'Load More'}
  </Button>
)}
```

#### Sent Email List Columns

| Column | Content |
|--------|---------|
| Date | Formatted datetime |
| From | Sender address |
| To | First recipient + count if multiple |
| Subject | Truncated subject line |

#### Email Detail View

Show:
- Message ID, sent timestamp
- From, To (all), CC (if present)
- Reply-To (if present)
- Subject
- Body tabs: HTML (rendered in iframe) | Text (monospace)

**Security**: Render HTML in sandboxed iframe:
```html
<iframe sandbox="allow-same-origin" srcdoc={sanitizedHtml} />
```

#### Research Insights: HTML Email Preview Security

**Defense in Depth Strategy:**
1. **DOMPurify sanitization** - Remove dangerous elements/attributes before rendering
2. **Sandboxed iframe** - Isolate content from parent page
3. **CSP meta tag** - Restrict resources inside iframe

**DOMPurify Configuration:**
```typescript
import DOMPurify from 'dompurify';

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
    ADD_ATTR: ['target'], // Allow target="_blank" for links
    FORBID_TAGS: ['script', 'style', 'iframe', 'form', 'input', 'object', 'embed'],
    FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover'],
  });
}
```

**Secure Iframe Component:**
```tsx
function EmailHtmlPreview({ html }: { html: string }) {
  const sanitizedHtml = sanitizeEmailHtml(html);

  // Wrap in minimal HTML document with CSP
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
      <body>${sanitizedHtml}</body>
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
```

**Edge Cases:**
- Handle null/empty htmlBody gracefully (show text body tab by default)
- Images may be blocked by CSP - show placeholder or allow https: sources
- Very long emails - set max-height with scroll
- RTL content - add `dir="auto"` to body tag

**Accessibility:**
- iframe: `title="Email HTML preview"` for screen readers
- Tab interface: Use proper `role="tablist"`, `role="tab"`, `role="tabpanel"`
- Text body: Use `<pre>` with `white-space: pre-wrap` for readability

### Phase 5: Navigation Update

#### client/src/App.tsx

Update `AuthenticatedApp` header:

```tsx
<div className="flex items-center gap-1">
  <Link
    to="/api-keys"
    className="inline-flex items-center gap-1 px-2 py-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
  >
    API Keys
  </Link>
  <Link
    to="/sent-emails"
    className="inline-flex items-center gap-1 px-2 py-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
  >
    Sent Emails
  </Link>
  {/* existing Jobs, Theme, Logout */}
</div>
```

Add routes:

```tsx
<Routes>
  <Route path="/" element={<DomainsPage />} />
  <Route path="/domains/:id" element={<DomainDetailPage />} />
  <Route path="/api-keys" element={<ApiKeysPage />} />
  <Route path="/sent-emails" element={<SentEmailsPage />} />
  <Route path="*" element={<Navigate to="/" replace />} />
</Routes>
```

## Acceptance Criteria

### API Keys

- [x] Can view list of all API keys with name, prefix, domains, dates
- [x] Can create new API key with name and domain selection
- [x] Full key is shown exactly once after creation with copy button
- [x] Cannot close key reveal modal without confirming copy
- [x] Can revoke API key with confirmation dialog
- [x] Revoked keys show visual indication (if displayed)
- [x] Admin keys show "Admin" badge
- [x] Empty state shows helpful message

### Sent Emails

- [x] Can view paginated list of sent emails
- [x] Can filter by domain (dropdown)
- [x] Can filter by date range
- [x] Can click email to see full details
- [x] Detail view shows HTML body safely (sandboxed)
- [x] Detail view shows text body alternative
- [x] BCC is never displayed (handled by backend)
- [x] Cursor pagination with "Load More" button
- [x] Empty state with appropriate message for filters

### Navigation

- [x] API Keys and Sent Emails links in header
- [ ] Links highlight when on respective page
- [x] Mobile-responsive header

## Files to Create/Modify

### New Files

- [x] `client/src/pages/ApiKeysPage.tsx`
- [x] `client/src/pages/SentEmailsPage.tsx`
- [x] `client/src/hooks/useApiKeys.ts`
- [x] `client/src/hooks/useSentEmails.ts`
- [x] `client/src/components/CreateApiKeyModal.tsx` (inline in ApiKeysPage.tsx)
- [x] `client/src/components/ApiKeyRevealModal.tsx` (inline in ApiKeysPage.tsx)
- [x] `client/src/components/DeleteApiKeyModal.tsx` (inline in ApiKeysPage.tsx)
- [x] `client/src/components/SentEmailFilters.tsx` (inline in SentEmailsPage.tsx)
- [x] `client/src/components/SentEmailDetailModal.tsx` (inline in SentEmailsPage.tsx)

### Modify

- [x] `client/src/lib/schemas.ts` - Add new schemas
- [x] `client/src/lib/api.ts` - Add new API methods
- [x] `client/src/App.tsx` - Add navigation and routes

## Dependencies

- Existing `/v1/api-keys` endpoints (working)
- Existing `/v1/sent-emails` endpoints (working)
- Existing `/v1/domains` endpoint for domain dropdown
- shadcn/ui components: Button, Card, Input, Dialog, Select, Table
- **DOMPurify** - npm package for HTML sanitization (`npm install dompurify @types/dompurify`)

## Open Questions (Resolved with Defaults)

1. **Navigation style?** → Header links (like "Jobs")
2. **Show revoked keys?** → Yes, with visual distinction
3. **Key reveal safety?** → Require checkbox confirmation before close
4. **HTML rendering?** → Sandboxed iframe
5. **Date filter UI?** → Simple inputs with presets
6. **Domain display on keys?** → Show 2 names + "+N more"

## References

### Existing Patterns

- `client/src/pages/DomainsPage.tsx` - List page pattern
- `client/src/pages/DomainDetailPage.tsx` - Detail page, delete modal pattern
- `client/src/hooks/useDomains.ts` - React Query hook pattern
- `client/src/lib/api.ts:93-108` - API client method pattern
- `client/src/lib/schemas.ts` - Zod schema pattern

### Backend Endpoints

- `src/SelfMX.Api/Endpoints/ApiKeyEndpoints.cs` - API key CRUD
- `src/SelfMX.Api/Endpoints/SentEmailEndpoints.cs` - Sent email list/detail
- `src/SelfMX.Api/Contracts/Responses/ApiResponses.cs:93-123` - Response types

### Documentation

- `/home/alex/Source/selfmx/plans/feat-multi-tenant-api-keys-audit-trail.md` - API key architecture
- `/home/alex/Source/selfmx/plans/2026-01-31-feat-sent-email-storage-retention-plan.md` - Sent email storage
