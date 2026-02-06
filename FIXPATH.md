# Resend SDK Compatibility Fixes

This document tracks fixes needed to make SelfMX fully compatible with the official Resend .NET SDK.

---

## Fix 1: Use Root-Level Paths (No /v1) ✅ DONE

### Problem

The official Resend SDKs build requests like:

```csharp
httpClient.BaseAddress = new Uri(opt.ApiUrl);  // e.g., "https://selfmx.example.com"
var req = new HttpRequestMessage(HttpMethod.Post, "/emails");
```

Any base URL that includes a path segment (like `/v1`) is ignored when the SDK sends a request with a leading `/`.

### Solution

Expose **all Resend-compatible endpoints at the root** (no `/v1` prefix).

### Status: ✅ Implemented

---

## Fix 2: Return GUID Format for Email ID ✅ DONE

### Problem

The Resend SDK deserializes the `/emails` response into this type:

```csharp
public class ObjectId
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }  // <-- Expects a GUID, not arbitrary string
}
```

SelfMX was returning the AWS SES message ID (e.g., `0107019c33b3e0b2-...`) which is **not** a valid GUID format. This caused:

```
Resend.ResendException: Failed deserializing response
```

### Solution

Return the internally-generated GUID (`sentEmail.Id`) instead of the SES message ID (`messageId`). The SES message ID is still stored in the database for tracking.

### Implementation

Changed `src/SelfMX.Api/Endpoints/EmailEndpoints.cs` line 147:

```diff
-return TypedResults.Ok(new SendEmailResponse(messageId));
+return TypedResults.Ok(new SendEmailResponse(sentEmail.Id));
```

### Status: ✅ Implemented

---

## Testing

```bash
# Should work with Resend-compatible clients
curl -X POST https://selfmx.example.com/emails -H "Authorization: Bearer re_xxx" -d '...'
# Response: {"id":"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"}  (valid GUID format)
```

## Configuration

Set SDK base URLs without a path:

```
Email__Resend__BaseUrl=https://selfmx.example.com
```
