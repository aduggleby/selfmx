# Fix: Use Root-Level Resend Paths (No /v1)

## Problem

The official Resend SDKs build requests like:

```csharp
httpClient.BaseAddress = new Uri(opt.ApiUrl);  // e.g., "https://selfmx.example.com"
var req = new HttpRequestMessage(HttpMethod.Post, "/emails");
```

Any base URL that includes a path segment (like `/v1`) is ignored when the SDK sends a request with a leading `/`.

## Solution

Expose **all Resend-compatible endpoints at the root** (no `/v1` prefix), and stop serving `/v1` routes entirely.

## Implementation

- Remove the `/v1` route group in `src/SelfMX.Api/Program.cs`.
- Map authenticated/admin endpoints directly at the root.
- Update client and docs to call root endpoints only (e.g., `/emails`, `/domains`, `/api-keys`).

## Testing

```bash
# Should work with Resend-compatible clients
curl -X POST https://selfmx.example.com/emails -H "Authorization: Bearer re_xxx" -d '...'
curl -X GET https://selfmx.example.com/domains -H "Authorization: Bearer re_xxx"
```

## Configuration

Set SDK base URLs without a path, for example:

```
Email__Resend__BaseUrl=https://selfmx.example.com
```
