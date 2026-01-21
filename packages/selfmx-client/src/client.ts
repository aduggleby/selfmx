import type {
  SendEmailRequest,
  SendEmailResponse,
  CreateDomainRequest,
  Domain,
  PaginatedResponse,
  SelfmxClientOptions,
  ApiError,
} from './types.js';

export class SelfmxError extends Error {
  public readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'SelfmxError';
    this.code = code;
  }
}

function toArray(value: string | string[] | undefined): string[] | undefined {
  if (value === undefined) return undefined;
  return Array.isArray(value) ? value : [value];
}

export class SelfmxClient {
  private readonly apiKey: string;
  private readonly baseUrl: string;

  constructor(options: SelfmxClientOptions) {
    this.apiKey = options.apiKey;
    this.baseUrl = options.baseUrl ?? 'http://localhost:5000';
  }

  private async request<T>(
    path: string,
    options: RequestInit = {}
  ): Promise<T> {
    const url = `${this.baseUrl}/v1${path}`;
    const response = await fetch(url, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${this.apiKey}`,
        ...options.headers,
      },
    });

    if (!response.ok) {
      let error: ApiError | null = null;
      try {
        error = await response.json();
      } catch {
        // Ignore JSON parse errors
      }

      throw new SelfmxError(
        error?.error?.code ?? 'unknown_error',
        error?.error?.message ?? `Request failed with status ${response.status}`
      );
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return response.json();
  }

  // Email methods
  readonly emails = {
    send: async (request: SendEmailRequest): Promise<SendEmailResponse> => {
      return this.request<SendEmailResponse>('/emails', {
        method: 'POST',
        body: JSON.stringify({
          from: request.from,
          to: toArray(request.to),
          subject: request.subject,
          html: request.html,
          text: request.text,
          cc: toArray(request.cc),
          bcc: toArray(request.bcc),
          replyTo: toArray(request.replyTo),
          headers: request.headers,
        }),
      });
    },
  };

  // Domain methods
  readonly domains = {
    list: async (
      page = 1,
      limit = 20
    ): Promise<PaginatedResponse<Domain>> => {
      return this.request<PaginatedResponse<Domain>>(
        `/domains?page=${page}&limit=${limit}`
      );
    },

    get: async (id: string): Promise<Domain> => {
      return this.request<Domain>(`/domains/${id}`);
    },

    create: async (request: CreateDomainRequest): Promise<Domain> => {
      return this.request<Domain>('/domains', {
        method: 'POST',
        body: JSON.stringify(request),
      });
    },

    delete: async (id: string): Promise<void> => {
      await this.request<void>(`/domains/${id}`, {
        method: 'DELETE',
      });
    },
  };
}
