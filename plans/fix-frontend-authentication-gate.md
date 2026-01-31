# fix: Frontend Authentication Gate

## Overview

The SelfMX demo at https://demo.selfmx.com is broken - the frontend loads without any authentication prompt, allowing users to see the main UI. However, all API requests fail with 401 Unauthorized because the backend requires authentication. The frontend should prompt for a password before showing protected content.

**Root Cause**: The backend has full cookie-based admin authentication support, but the frontend:
1. Has no login UI
2. Does not send cookies with requests (missing `credentials: 'include'`)
3. Only checks for a non-existent `VITE_API_KEY` environment variable

## Problem Statement

### Current Behavior
1. User visits https://demo.selfmx.com
2. Main UI loads immediately (DomainsPage with "Add your first domain")
3. API call to `GET /v1/domains?page=1&limit=10` returns 401
4. Toast shows "Unable to load domains - Request failed with status 401"
5. User cannot do anything - all API calls fail

### Expected Behavior
1. User visits https://demo.selfmx.com
2. App checks authentication status via `GET /v1/admin/me`
3. If not authenticated → Show login page with password input
4. User enters password → `POST /v1/admin/login` validates against `AdminPasswordHash`
5. On success → Cookie set, redirect to main app
6. On failure → Show error message, stay on login page

## Technical Approach

### Backend (Already Implemented)

The backend has complete authentication support:

| Component | Location | Description |
|-----------|----------|-------------|
| Cookie Auth | `Program.cs:110-129` | 30-day sliding expiration, HttpOnly, SameSite=Strict |
| Login Endpoint | `AdminEndpoints.cs:23-70` | `POST /v1/admin/login` with BCrypt validation |
| Session Check | `AdminEndpoints.cs` | `GET /v1/admin/me` returns admin info if authenticated |
| Logout | `AdminEndpoints.cs` | `POST /v1/admin/logout` clears cookie |
| Rate Limiting | `Program.cs:258-260` | 5 requests/minute on login endpoint |

### Frontend Changes Required

#### Phase 1: Enable Cookie Authentication

**File: `client/src/lib/api.ts`**

Add `credentials: 'include'` to all fetch requests:

```typescript
// In the request() method
const response = await fetch(`${this.baseUrl}${path}`, {
  ...options,
  credentials: 'include', // ADD THIS
  headers: {
    'Content-Type': 'application/json',
    ...this.getAuthHeaders(),
    ...options?.headers,
  },
});
```

Also fix `deleteDomain()` which has its own fetch call:

```typescript
async deleteDomain(id: string): Promise<void> {
  const response = await fetch(`${this.baseUrl}/v1/domains/${id}`, {
    method: 'DELETE',
    credentials: 'include', // ADD THIS
    headers: this.getAuthHeaders(),
  });
  // ...
}
```

Add new auth-related methods:

```typescript
async login(password: string): Promise<void> {
  const response = await fetch(`${this.baseUrl}/v1/admin/login`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    const message = error?.error?.message || 'Login failed';
    const err = new Error(message) as Error & { status: number };
    err.status = response.status;
    throw err;
  }
}

async logout(): Promise<void> {
  await fetch(`${this.baseUrl}/v1/admin/logout`, {
    method: 'POST',
    credentials: 'include',
  });
}

async checkAuth(): Promise<{ email: string }> {
  const response = await fetch(`${this.baseUrl}/v1/admin/me`, {
    credentials: 'include',
  });

  if (!response.ok) {
    const err = new Error('Not authenticated') as Error & { status: number };
    err.status = response.status;
    throw err;
  }

  return response.json();
}
```

#### Phase 2: Authentication Context

**New File: `client/src/contexts/AuthContext.tsx`**

```typescript
import { createContext, useContext, useState, useCallback, useEffect, ReactNode } from 'react';
import { api } from '@/lib/api';

type AuthState = 'loading' | 'authenticated' | 'unauthenticated';

interface AuthContextType {
  state: AuthState;
  error: string | null;
  login: (password: string) => Promise<boolean>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>('loading');
  const [error, setError] = useState<string | null>(null);

  // Check auth on mount
  useEffect(() => {
    api.checkAuth()
      .then(() => setState('authenticated'))
      .catch(() => setState('unauthenticated'));
  }, []);

  const login = useCallback(async (password: string): Promise<boolean> => {
    setError(null);
    try {
      await api.login(password);
      setState('authenticated');
      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed';
      setError(message);
      return false;
    }
  }, []);

  const logout = useCallback(async () => {
    await api.logout();
    setState('unauthenticated');
    setError(null);
  }, []);

  return (
    <AuthContext.Provider value={{ state, error, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
}
```

#### Phase 3: Login Page Component

**New File: `client/src/pages/LoginPage.tsx`**

```typescript
import { useState, FormEvent } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';

export function LoginPage() {
  const { login, error } = useAuth();
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!password.trim()) return;

    setIsLoading(true);
    await login(password);
    setIsLoading(false);
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">SelfMX</CardTitle>
          <CardDescription>Enter your admin password to continue</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <Input
              type="password"
              placeholder="Password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoFocus
              disabled={isLoading}
            />
            {error && (
              <p className="text-sm text-destructive">{error}</p>
            )}
            <Button
              type="submit"
              className="w-full"
              disabled={!password.trim() || isLoading}
            >
              {isLoading ? 'Signing in...' : 'Sign In'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
```

#### Phase 4: Update App.tsx

**File: `client/src/App.tsx`**

```typescript
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from '@/components/ui/toaster';
import { ThemeProvider } from '@/components/ui/theme-provider';
import { AuthProvider, useAuth } from '@/contexts/AuthContext';
import { DomainsPage } from '@/pages/DomainsPage';
import { LoginPage } from '@/pages/LoginPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        // Don't retry on 401/403
        if ((error as Error & { status?: number }).status === 401) return false;
        if ((error as Error & { status?: number }).status === 403) return false;
        return failureCount < 2;
      },
    },
  },
});

function AppContent() {
  const { state } = useAuth();

  if (state === 'loading') {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-pulse text-muted-foreground">Loading...</div>
      </div>
    );
  }

  if (state === 'unauthenticated') {
    return <LoginPage />;
  }

  return <DomainsPage />;
}

function App() {
  return (
    <ThemeProvider defaultTheme="system" storageKey="selfmx-ui-theme">
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <AppContent />
          <Toaster />
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}

export default App;
```

#### Phase 5: Add Logout Button to Header

**File: `client/src/components/Header.tsx`** (or inline in DomainsPage)

Add a logout button next to the theme toggle:

```typescript
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { LogOut } from 'lucide-react';

export function Header() {
  const { logout } = useAuth();

  return (
    <header className="...">
      {/* existing content */}
      <div className="flex items-center gap-2">
        <ThemeToggle />
        <Button variant="ghost" size="icon" onClick={logout} title="Logout">
          <LogOut className="h-5 w-5" />
        </Button>
      </div>
    </header>
  );
}
```

#### Phase 6: Global 401 Error Handling

**Update: `client/src/lib/api.ts`**

Add status code to errors for global handling:

```typescript
if (!response.ok) {
  let message = `Request failed with status ${response.status}`;
  try {
    const errorBody = await response.json();
    message = errorBody?.error?.message || message;
  } catch {}

  const error = new Error(message) as Error & { status: number };
  error.status = response.status;
  throw error;
}
```

**Update: `client/src/contexts/AuthContext.tsx`**

Listen for 401 errors globally:

```typescript
// In AuthProvider, add effect to handle 401 from any API call
useEffect(() => {
  const handleUnauthorized = () => setState('unauthenticated');
  window.addEventListener('selfmx:unauthorized', handleUnauthorized);
  return () => window.removeEventListener('selfmx:unauthorized', handleUnauthorized);
}, []);
```

**Update: `client/src/lib/api.ts`**

Dispatch event on 401:

```typescript
if (response.status === 401) {
  window.dispatchEvent(new Event('selfmx:unauthorized'));
}
```

## Acceptance Criteria

### Functional Requirements

- [x] Unauthenticated users see login page on first visit
- [x] Valid password grants access to main app
- [x] Invalid password shows error message, stays on login page
- [x] Authenticated users see main app on return visit (cookie persists)
- [x] Logout button clears session and returns to login
- [x] Session expiry mid-use redirects to login on next API call
- [x] Rate limiting (5 attempts/min) shows appropriate error

### Non-Functional Requirements

- [x] Loading state shown during initial auth check (no flash of content)
- [x] Login form has proper focus management (autofocus password field)
- [x] Error messages are accessible (associated with input)
- [x] Works with existing dark/light theme

### Quality Gates

- [x] All existing Playwright tests still pass
- [x] New E2E tests for authentication flows pass
- [x] No TypeScript errors
- [ ] No ESLint errors (pre-existing issues remain)

## Playwright Test Plan

### Test File: `client/e2e/auth.spec.ts`

```typescript
import { test, expect } from '@playwright/test';

test.describe('Authentication', () => {
  test.describe('Login Flow', () => {
    test('shows login page when not authenticated', async ({ page }) => {
      await page.goto('/');
      await expect(page.getByRole('heading', { name: 'SelfMX' })).toBeVisible();
      await expect(page.getByPlaceholder('Password')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Sign In' })).toBeVisible();
    });

    test('successful login shows main app', async ({ page }) => {
      await page.goto('/');
      await page.getByPlaceholder('Password').fill('test-password');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByRole('heading', { name: /domains/i })).toBeVisible();
    });

    test('invalid password shows error', async ({ page }) => {
      await page.goto('/');
      await page.getByPlaceholder('Password').fill('wrong-password');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByText(/invalid/i)).toBeVisible();
      await expect(page.getByPlaceholder('Password')).toBeVisible();
    });

    test('empty password disables submit button', async ({ page }) => {
      await page.goto('/');
      await expect(page.getByRole('button', { name: 'Sign In' })).toBeDisabled();
    });

    test('shows loading state during login', async ({ page }) => {
      await page.route('**/v1/admin/login', async (route) => {
        await new Promise(r => setTimeout(r, 500));
        await route.continue();
      });

      await page.goto('/');
      await page.getByPlaceholder('Password').fill('test-password');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByRole('button', { name: 'Signing in...' })).toBeVisible();
    });
  });

  test.describe('Session Persistence', () => {
    test('authenticated user sees main app on refresh', async ({ page }) => {
      // First login
      await page.goto('/');
      await page.getByPlaceholder('Password').fill('test-password');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByRole('heading', { name: /domains/i })).toBeVisible();

      // Refresh should stay authenticated
      await page.reload();
      await expect(page.getByRole('heading', { name: /domains/i })).toBeVisible();
    });
  });

  test.describe('Logout Flow', () => {
    test('logout returns to login page', async ({ page }) => {
      // Login first
      await page.goto('/');
      await page.getByPlaceholder('Password').fill('test-password');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByRole('heading', { name: /domains/i })).toBeVisible();

      // Logout
      await page.getByRole('button', { name: 'Logout' }).click();
      await expect(page.getByPlaceholder('Password')).toBeVisible();
    });
  });

  test.describe('Error Handling', () => {
    test('401 on API call redirects to login', async ({ page }) => {
      // Mock auth check to succeed initially
      let authCallCount = 0;
      await page.route('**/v1/admin/me', async (route) => {
        authCallCount++;
        if (authCallCount === 1) {
          await route.fulfill({ status: 200, body: JSON.stringify({ email: 'admin' }) });
        } else {
          await route.fulfill({ status: 401 });
        }
      });

      // Mock domains call to fail with 401
      await page.route('**/v1/domains*', async (route) => {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: { message: 'Session expired' } }),
        });
      });

      await page.goto('/');
      // Should redirect to login after 401
      await expect(page.getByPlaceholder('Password')).toBeVisible();
    });

    test('rate limiting shows appropriate error', async ({ page }) => {
      await page.route('**/v1/admin/login', async (route) => {
        await route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({ error: { message: 'Too many requests' } }),
        });
      });

      await page.goto('/');
      await page.getByPlaceholder('Password').fill('test');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByText(/too many/i)).toBeVisible();
    });

    test('server error shows appropriate message', async ({ page }) => {
      await page.route('**/v1/admin/login', async (route) => {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: { message: 'Internal server error' } }),
        });
      });

      await page.goto('/');
      await page.getByPlaceholder('Password').fill('test');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByText(/error/i)).toBeVisible();
    });

    test('network error shows appropriate message', async ({ page }) => {
      await page.route('**/v1/admin/login', async (route) => {
        await route.abort('connectionrefused');
      });

      await page.goto('/');
      await page.getByPlaceholder('Password').fill('test');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await expect(page.getByText(/failed|error|unavailable/i)).toBeVisible();
    });
  });

  test.describe('Loading States', () => {
    test('shows loading during initial auth check', async ({ page }) => {
      await page.route('**/v1/admin/me', async (route) => {
        await new Promise(r => setTimeout(r, 500));
        await route.fulfill({ status: 401 });
      });

      await page.goto('/');
      await expect(page.getByText('Loading...')).toBeVisible();
      await expect(page.getByPlaceholder('Password')).toBeVisible();
    });
  });
});
```

### Test File: `client/e2e/auth-integration.spec.ts`

Tests that run against the real backend (not mocked):

```typescript
import { test, expect } from '@playwright/test';

test.describe('Authentication Integration', () => {
  // These tests require the backend to be running with a known test password
  // Set via environment variable: TEST_ADMIN_PASSWORD

  const TEST_PASSWORD = process.env.TEST_ADMIN_PASSWORD || 'test-password';

  test.beforeEach(async ({ page }) => {
    // Clear cookies to ensure clean state
    await page.context().clearCookies();
  });

  test('full login flow with real backend', async ({ page }) => {
    await page.goto('/');

    // Should see login
    await expect(page.getByPlaceholder('Password')).toBeVisible();

    // Login with real password
    await page.getByPlaceholder('Password').fill(TEST_PASSWORD);
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Should see main app
    await expect(page.getByRole('heading', { name: /domains/i })).toBeVisible({ timeout: 10000 });
  });

  test('invalid password rejected by real backend', async ({ page }) => {
    await page.goto('/');
    await page.getByPlaceholder('Password').fill('definitely-wrong-password');
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Should show error and stay on login
    await expect(page.getByText(/invalid/i)).toBeVisible();
    await expect(page.getByPlaceholder('Password')).toBeVisible();
  });
});
```

## Files to Create/Modify

| File | Action | Description |
|------|--------|-------------|
| `client/src/lib/api.ts` | Modify | Add `credentials: 'include'`, login/logout/checkAuth methods, status codes on errors |
| `client/src/contexts/AuthContext.tsx` | Create | Auth state management with login/logout |
| `client/src/pages/LoginPage.tsx` | Create | Login form UI |
| `client/src/App.tsx` | Modify | Wrap in AuthProvider, conditional rendering based on auth state |
| `client/src/pages/DomainsPage.tsx` | Modify | Add logout button to header |
| `client/e2e/auth.spec.ts` | Create | E2E tests for auth flows |
| `client/e2e/auth-integration.spec.ts` | Create | Integration tests with real backend |

## Dependencies & Prerequisites

- Backend already has all required endpoints implemented
- No new npm packages needed (using existing React Context, fetch API)
- Requires `AdminPasswordHash` to be configured in backend settings

## Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| CORS issues with credentials | Medium | High | Verify backend CORS config allows credentials from frontend origin |
| Cookie not set due to SameSite | Low | High | Backend uses SameSite=Strict which should work for same-origin |
| Breaking existing API key auth | Low | Medium | Keep Bearer token auth path, cookies are additive |
| Race condition in auth check | Low | Medium | Show loading state during check, don't render content until resolved |

## References

### Internal
- Backend auth implementation: `src/SelfMX.Api/Endpoints/AdminEndpoints.cs`
- Cookie configuration: `src/SelfMX.Api/Program.cs:110-129`
- Rate limiting: `src/SelfMX.Api/Program.cs:258-260`
- Existing API client: `client/src/lib/api.ts`

### External
- [Playwright Authentication Testing](https://playwright.dev/docs/auth)
- [TanStack Query Error Handling](https://tanstack.com/query/latest/docs/framework/react/guides/query-functions#usage-with-fetch-and-other-clients-that-do-not-throw-by-default)
- [React Context Best Practices](https://react.dev/learn/passing-data-deeply-with-context)
