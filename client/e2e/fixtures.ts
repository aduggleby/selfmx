import { test as base, Page } from '@playwright/test';

// Mock data types matching the API schemas
export interface DnsRecord {
  type: string;
  name: string;
  value: string;
  priority: number;
  verified: boolean;
}

export interface Domain {
  id: string;
  name: string;
  status: 'pending' | 'verifying' | 'verified' | 'failed';
  createdAt: string;
  verifiedAt: string | null;
  failureReason: string | null;
  dnsRecords: DnsRecord[] | null;
  lastCheckedAt: string | null;
  nextCheckAt: string | null;
}

export interface PaginatedDomains {
  data: Domain[];
  page: number;
  limit: number;
  total: number;
}

// Mock data factory functions
export function createMockDomain(overrides: Partial<Domain> = {}): Domain {
  const status = overrides.status ?? 'pending';
  // Calculate next check time (next 5-minute interval)
  const now = new Date();
  const minutes = now.getMinutes();
  const nextMinute = (Math.floor(minutes / 5) + 1) * 5;
  const nextCheckAt = nextMinute >= 60
    ? new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours() + 1, 0, 0).toISOString()
    : new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours(), nextMinute, 0).toISOString();

  return {
    id: `domain-${Math.random().toString(36).substring(7)}`,
    name: 'example.com',
    status,
    createdAt: new Date().toISOString(),
    verifiedAt: null,
    failureReason: null,
    dnsRecords: null,
    lastCheckedAt: status === 'verifying' ? new Date(Date.now() - 60000).toISOString() : null,
    nextCheckAt: status === 'verifying' ? nextCheckAt : null,
    ...overrides,
  };
}

export function createMockDnsRecords(domain: string): DnsRecord[] {
  return [
    {
      type: 'CNAME',
      name: `token1._domainkey.${domain}`,
      value: 'token1.dkim.amazonses.com',
      priority: 0,
      verified: false,
    },
    {
      type: 'CNAME',
      name: `token2._domainkey.${domain}`,
      value: 'token2.dkim.amazonses.com',
      priority: 0,
      verified: false,
    },
    {
      type: 'CNAME',
      name: `token3._domainkey.${domain}`,
      value: 'token3.dkim.amazonses.com',
      priority: 0,
      verified: false,
    },
    {
      type: 'TXT',
      name: domain,
      value: 'v=spf1 include:amazonses.com ~all',
      priority: 0,
      verified: false,
    },
    {
      type: 'TXT',
      name: `_dmarc.${domain}`,
      value: 'v=DMARC1; p=none;',
      priority: 0,
      verified: false,
    },
  ];
}

// API Mock handler class
export class ApiMock {
  private domains: Map<string, Domain> = new Map();
  private page: Page;
  private createDomainHandler: ((name: string) => Domain | { error: { code: string; message: string } }) | null = null;
  private deleteDomainHandler: ((id: string) => boolean) | null = null;
  private isAuthenticated = true;
  private loginHandler: ((password: string) => boolean) | null = null;

  constructor(page: Page) {
    this.page = page;
  }

  // Set authentication state
  setAuthenticated(authenticated: boolean) {
    this.isAuthenticated = authenticated;
  }

  // Custom login handler for testing specific scenarios
  onLogin(handler: (password: string) => boolean) {
    this.loginHandler = handler;
  }

  // Set initial domains
  setDomains(domains: Domain[]) {
    this.domains.clear();
    domains.forEach(d => this.domains.set(d.id, d));
  }

  // Custom handlers for testing specific scenarios
  onCreateDomain(handler: (name: string) => Domain | { error: { code: string; message: string } }) {
    this.createDomainHandler = handler;
  }

  onDeleteDomain(handler: (id: string) => boolean) {
    this.deleteDomainHandler = handler;
  }

  // Setup all route handlers
  async setup() {
    // System status (always healthy in tests)
    await this.page.route('**/system/status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          healthy: true,
          issues: [],
          timestamp: new Date().toISOString(),
        }),
      });
    });

    // Auth check
    await this.page.route('**/admin/me', async (route) => {
      if (this.isAuthenticated) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ email: 'admin@example.com' }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: { code: 'unauthorized', message: 'Not authenticated' } }),
        });
      }
    });

    // Login
    await this.page.route('**/admin/login', async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue();
        return;
      }

      const body = route.request().postDataJSON();
      const password = body?.password;

      if (this.loginHandler) {
        const success = this.loginHandler(password);
        if (success) {
          this.isAuthenticated = true;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ message: 'Login successful' }),
          });
        } else {
          await route.fulfill({
            status: 401,
            contentType: 'application/json',
            body: JSON.stringify({ error: { code: 'invalid_credentials', message: 'Invalid password' } }),
          });
        }
        return;
      }

      // Default: accept any non-empty password
      if (password && password.length > 0) {
        this.isAuthenticated = true;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'Login successful' }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: { code: 'invalid_credentials', message: 'Invalid password' } }),
        });
      }
    });

    // Logout
    await this.page.route('**/admin/logout', async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue();
        return;
      }

      this.isAuthenticated = false;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Logged out' }),
      });
    });

    // List domains
    await this.page.route('**/domains?*', async (route) => {
      const fetchDest = route.request().headers()['sec-fetch-dest'] ?? '';
      const fetchMode = route.request().headers()['sec-fetch-mode'] ?? '';
      if (fetchDest === 'document' || fetchMode === 'navigate') {
        await route.continue();
        return;
      }
      const accept = route.request().headers()['accept'] ?? '';
      if (accept.includes('text/html')) {
        await route.continue();
        return;
      }
      const resourceType = route.request().resourceType();
      if (resourceType !== 'xhr' && resourceType !== 'fetch') {
        await route.continue();
        return;
      }
      const url = new URL(route.request().url());
      const page = parseInt(url.searchParams.get('page') || '1');
      const limit = parseInt(url.searchParams.get('limit') || '20');

      const allDomains = Array.from(this.domains.values());
      const start = (page - 1) * limit;
      const end = start + limit;
      const paginatedDomains = allDomains.slice(start, end);

      const response: PaginatedDomains = {
        data: paginatedDomains,
        page,
        limit,
        total: allDomains.length,
      };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(response),
      });
    });

    // Create domain
    await this.page.route('**/domains', async (route) => {
      const fetchDest = route.request().headers()['sec-fetch-dest'] ?? '';
      const fetchMode = route.request().headers()['sec-fetch-mode'] ?? '';
      if (fetchDest === 'document' || fetchMode === 'navigate') {
        await route.continue();
        return;
      }
      const accept = route.request().headers()['accept'] ?? '';
      if (accept.includes('text/html')) {
        await route.continue();
        return;
      }
      const resourceType = route.request().resourceType();
      if (resourceType !== 'xhr' && resourceType !== 'fetch') {
        await route.continue();
        return;
      }
      if (route.request().method() !== 'POST') {
        await route.continue();
        return;
      }

      const body = route.request().postDataJSON();
      const name = body?.name;

      if (this.createDomainHandler) {
        const result = this.createDomainHandler(name);
        if ('error' in result) {
          await route.fulfill({
            status: 409,
            contentType: 'application/json',
            body: JSON.stringify(result),
          });
          return;
        }
        this.domains.set(result.id, result);
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(result),
        });
        return;
      }

      // Default create behavior
      const newDomain = createMockDomain({
        name,
        status: 'pending',
        dnsRecords: createMockDnsRecords(name),
      });
      this.domains.set(newDomain.id, newDomain);

      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(newDomain),
      });
    });

    // Get single domain
    await this.page.route('**/domains/*', async (route) => {
      const fetchDest = route.request().headers()['sec-fetch-dest'] ?? '';
      const fetchMode = route.request().headers()['sec-fetch-mode'] ?? '';
      if (fetchDest === 'document' || fetchMode === 'navigate') {
        await route.continue();
        return;
      }
      const accept = route.request().headers()['accept'] ?? '';
      if (accept.includes('text/html')) {
        await route.continue();
        return;
      }
      const resourceType = route.request().resourceType();
      if (resourceType !== 'xhr' && resourceType !== 'fetch') {
        await route.continue();
        return;
      }
      const method = route.request().method();
      const url = route.request().url();
      const idMatch = url.match(/\/domains\/([^?/]+)/);
      const id = idMatch?.[1];

      if (method === 'GET' && id) {
        const domain = this.domains.get(id);
        if (domain) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(domain),
          });
        } else {
          await route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ error: { code: 'not_found', message: 'Domain not found' } }),
          });
        }
        return;
      }

      if (method === 'DELETE' && id) {
        if (this.deleteDomainHandler) {
          const success = this.deleteDomainHandler(id);
          if (!success) {
            await route.fulfill({
              status: 500,
              contentType: 'application/json',
              body: JSON.stringify({ error: { code: 'delete_failed', message: 'Failed to delete domain' } }),
            });
            return;
          }
        }

        this.domains.delete(id);
        await route.fulfill({
          status: 204,
          body: '',
        });
        return;
      }

      await route.continue();
    });
  }
}

// Extended test fixture with API mocking
export const test = base.extend<{ apiMock: ApiMock }>({
  apiMock: async ({ page }, use) => {
    const apiMock = new ApiMock(page);
    await apiMock.setup();
    await use(apiMock);
  },
});

export { expect } from '@playwright/test';
