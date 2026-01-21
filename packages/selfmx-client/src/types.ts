export interface SendEmailRequest {
  from: string;
  to: string | string[];
  subject: string;
  html?: string;
  text?: string;
  cc?: string | string[];
  bcc?: string | string[];
  replyTo?: string | string[];
  headers?: Record<string, string>;
}

export interface SendEmailResponse {
  id: string;
}

export interface CreateDomainRequest {
  name: string;
}

export type DomainStatus = 'pending' | 'verifying' | 'verified' | 'failed';

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
  status: DomainStatus;
  createdAt: string;
  verifiedAt: string | null;
  failureReason: string | null;
  dnsRecords: DnsRecord[] | null;
}

export interface PaginatedResponse<T> {
  data: T[];
  page: number;
  limit: number;
  total: number;
}

export interface ApiError {
  error: {
    code: string;
    message: string;
  };
}

export interface SelfmxClientOptions {
  apiKey: string;
  baseUrl?: string;
}
