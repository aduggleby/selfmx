import { z } from 'zod';
import {
  DomainSchema,
  PaginatedDomainsSchema,
  SendEmailResponseSchema,
  type Domain,
  type PaginatedDomains,
  type SendEmailResponse,
} from './schemas';

const API_BASE = '/v1';

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
    return schema.parse(data);
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
        // Ignore JSON parse errors and keep fallback message.
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
}

export const api = new ApiClient();
