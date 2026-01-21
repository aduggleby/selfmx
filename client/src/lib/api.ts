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

    const response = await fetch(`${API_BASE}${path}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error?.error?.message || 'Request failed');
    }

    const data = await response.json();
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

    const response = await fetch(`${API_BASE}/domains/${id}`, {
      method: 'DELETE',
      headers,
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error?.error?.message || 'Delete failed');
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
}

export const api = new ApiClient();
