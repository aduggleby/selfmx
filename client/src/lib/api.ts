import { z } from 'zod';
import {
  DomainSchema,
  PaginatedDomainsSchema,
  SendEmailResponseSchema,
  PaginatedApiKeysSchema,
  PaginatedRevokedApiKeysSchema,
  ApiKeyCreatedSchema,
  CursorPagedSentEmailsSchema,
  SentEmailDetailSchema,
  type Domain,
  type PaginatedDomains,
  type SendEmailResponse,
  type PaginatedApiKeys,
  type PaginatedRevokedApiKeys,
  type ApiKeyCreated,
  type CursorPagedSentEmails,
  type SentEmailDetail,
} from './schemas';

const API_BASE = '';

const getApiUrl = (path: string) => {
  if (typeof window === 'undefined') return `${API_BASE}${path}`;
  return new URL(`${API_BASE}${path}`, window.location.origin).toString();
};

const getApiHost = (path: string) => {
  if (typeof window === 'undefined') return 'server';
  return new URL(`${API_BASE}${path}`, window.location.origin).host;
};

class ApiClient {
  private apiKey: string | null = null;

  setApiKey(key: string) {
    this.apiKey = key;
  }

  private async request<T>(
    path: string,
    schema: z.ZodType<T>,
    options: RequestInit = {}
  ): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`;
    }

    const requestUrl = getApiUrl(path);
    const apiHost = getApiHost(path);
    let response: Response;

    try {
      response = await fetch(requestUrl, {
        ...options,
        credentials: 'include',
        headers,
      });
    } catch {
      throw new Error(
        `API unreachable at ${apiHost}. Check that the server and port are running.`
      );
    }

    if (!response.ok) {
      if (response.status === 401) {
        window.dispatchEvent(new Event('selfmx:unauthorized'));
      }
      let message = `Request failed with status ${response.status} from ${apiHost}.`;
      try {
        const errorBody = await response.json();
        message = errorBody?.error?.message || message;
      } catch {
        // Ignore JSON parse errors and keep fallback message.
      }
      const err = new Error(message) as Error & { status: number };
      err.status = response.status;
      throw err;
    }

    let data: unknown;
    try {
      data = await response.json();
    } catch {
      throw new Error(
        `API returned invalid JSON from ${apiHost}. Check that the server and port are running.`
      );
    }

    try {
      return schema.parse(data);
    } catch (zodError) {
      console.error('API response validation failed:', zodError);
      console.error('Response data:', data);
      throw zodError;
    }
  }

  async listDomains(page = 1, limit = 20): Promise<PaginatedDomains> {
    return this.request(
      `/domains?page=${page}&limit=${limit}`,
      PaginatedDomainsSchema
    );
  }

  async getDomain(id: string): Promise<Domain> {
    return this.request(`/domains/${id}`, DomainSchema);
  }

  async createDomain(name: string): Promise<Domain> {
    return this.request(`/domains`, DomainSchema, {
      method: 'POST',
      body: JSON.stringify({ name }),
    });
  }

  async deleteDomain(id: string): Promise<void> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`;
    }

    const requestUrl = getApiUrl(`/domains/${id}`);
    const apiHost = getApiHost(`/domains/${id}`);
    let response: Response;

    try {
      response = await fetch(requestUrl, {
        method: 'DELETE',
        credentials: 'include',
        headers,
      });
    } catch {
      throw new Error(
        `API unreachable at ${apiHost}. Check that the server and port are running.`
      );
    }

    if (!response.ok) {
      if (response.status === 401) {
        window.dispatchEvent(new Event('selfmx:unauthorized'));
      }
      let message = `Delete failed with status ${response.status} from ${apiHost}.`;
      try {
        const errorBody = await response.json();
        message = errorBody?.error?.message || message;
      } catch {
        // Ignore JSON parse errors
      }
      const err = new Error(message) as Error & { status: number };
      err.status = response.status;
      throw err;
    }
  }

  async sendEmail(request: {
    from: string;
    to: string[];
    subject: string;
    html?: string;
    text?: string;
  }): Promise<SendEmailResponse> {
    return this.request(`/emails`, SendEmailResponseSchema, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async sendTestEmail(
    domainId: string,
    request: { senderPrefix: string; to: string; subject: string; text: string }
  ): Promise<SendEmailResponse> {
    return this.request(`/domains/${domainId}/test-email`, SendEmailResponseSchema, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async verifyDomain(id: string): Promise<Domain> {
    return this.request(`/domains/${id}/verify`, DomainSchema, {
      method: 'POST',
    });
  }

  async login(password: string): Promise<void> {
    const requestUrl = getApiUrl('/admin/login');
    const apiHost = getApiHost('/admin/login');
    let response: Response;

    try {
      response = await fetch(requestUrl, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password }),
      });
    } catch {
      throw new Error(
        `API unreachable at ${apiHost}. Check that the server and port are running.`
      );
    }

    if (!response.ok) {
      let message = 'Login failed';
      try {
        const errorBody = await response.json();
        message = errorBody?.error?.message || message;
      } catch {
        // Ignore JSON parse errors
      }
      const err = new Error(message) as Error & { status: number };
      err.status = response.status;
      throw err;
    }
  }

  async logout(): Promise<void> {
    const requestUrl = getApiUrl('/admin/logout');
    try {
      await fetch(requestUrl, {
        method: 'POST',
        credentials: 'include',
      });
    } catch {
      // Ignore logout errors - user should still be logged out locally
    }
  }

  async checkAuth(): Promise<{ email: string }> {
    const requestUrl = getApiUrl('/admin/me');
    const apiHost = getApiHost('/admin/me');
    let response: Response;

    try {
      response = await fetch(requestUrl, {
        credentials: 'include',
      });
    } catch {
      throw new Error(
        `API unreachable at ${apiHost}. Check that the server and port are running.`
      );
    }

    if (!response.ok) {
      const err = new Error('Not authenticated') as Error & { status: number };
      err.status = response.status;
      throw err;
    }

    return response.json();
  }

  // API Key methods
  async listApiKeys(page = 1, limit = 20): Promise<PaginatedApiKeys> {
    return this.request(
      `/api-keys?page=${page}&limit=${limit}`,
      PaginatedApiKeysSchema
    );
  }

  async listRevokedApiKeys(page = 1, limit = 20): Promise<PaginatedRevokedApiKeys> {
    return this.request(
      `/api-keys/revoked?page=${page}&limit=${limit}`,
      PaginatedRevokedApiKeysSchema
    );
  }

  async createApiKey(request: {
    name: string;
    domainIds?: string[];
    isAdmin?: boolean;
  }): Promise<ApiKeyCreated> {
    return this.request(`/api-keys`, ApiKeyCreatedSchema, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async deleteApiKey(id: string): Promise<void> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`;
    }

    const requestUrl = getApiUrl(`/api-keys/${id}`);
    const apiHost = getApiHost(`/api-keys/${id}`);
    let response: Response;

    try {
      response = await fetch(requestUrl, {
        method: 'DELETE',
        credentials: 'include',
        headers,
      });
    } catch {
      throw new Error(
        `API unreachable at ${apiHost}. Check that the server and port are running.`
      );
    }

    if (!response.ok) {
      if (response.status === 401) {
        window.dispatchEvent(new Event('selfmx:unauthorized'));
      }
      let message = `Delete failed with status ${response.status} from ${apiHost}.`;
      try {
        const errorBody = await response.json();
        message = errorBody?.error?.message || message;
      } catch {
        // Ignore JSON parse errors
      }
      const err = new Error(message) as Error & { status: number };
      err.status = response.status;
      throw err;
    }
  }

  // Sent Email methods
  async listSentEmails(params: {
    domainId?: string;
    from?: string;
    to?: string;
    cursor?: string;
    pageSize?: number;
  } = {}): Promise<CursorPagedSentEmails> {
    const searchParams = new URLSearchParams();
    if (params.domainId) searchParams.set('domainId', params.domainId);
    if (params.from) searchParams.set('from', params.from);
    if (params.to) searchParams.set('to', params.to);
    if (params.cursor) searchParams.set('cursor', params.cursor);
    if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());

    const query = searchParams.toString();
    return this.request(
      `/sent-emails${query ? `?${query}` : ''}`,
      CursorPagedSentEmailsSchema
    );
  }

  async getSentEmail(id: string): Promise<SentEmailDetail> {
    return this.request(`/sent-emails/${id}`, SentEmailDetailSchema);
  }
}

export const api = new ApiClient();
