import { z } from 'zod';

export const DomainStatusSchema = z.enum(['pending', 'verifying', 'verified', 'failed']);
export type DomainStatus = z.infer<typeof DomainStatusSchema>;

export const DnsRecordSchema = z.object({
  type: z.string(),
  name: z.string(),
  value: z.string(),
  priority: z.number(),
  verified: z.boolean(),
});
export type DnsRecord = z.infer<typeof DnsRecordSchema>;

export const DomainSchema = z.object({
  id: z.string(),
  name: z.string(),
  status: DomainStatusSchema,
  createdAt: z.string().datetime(),
  verifiedAt: z.string().datetime().nullable(),
  failureReason: z.string().nullable(),
  dnsRecords: z.array(DnsRecordSchema).nullable(),
  lastCheckedAt: z.string().datetime().nullable(),
  nextCheckAt: z.string().datetime().nullable(),
});
export type Domain = z.infer<typeof DomainSchema>;

export const PaginatedDomainsSchema = z.object({
  data: z.array(DomainSchema),
  page: z.number(),
  limit: z.number(),
  total: z.number(),
});
export type PaginatedDomains = z.infer<typeof PaginatedDomainsSchema>;

export const ApiErrorSchema = z.object({
  error: z.object({
    code: z.string(),
    message: z.string(),
  }),
});
export type ApiError = z.infer<typeof ApiErrorSchema>;

export const SendEmailResponseSchema = z.object({
  id: z.string(),
});
export type SendEmailResponse = z.infer<typeof SendEmailResponseSchema>;

export const HealthResponseSchema = z.object({
  status: z.string(),
  timestamp: z.string().datetime(),
});
export type HealthResponse = z.infer<typeof HealthResponseSchema>;

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
  key: z.string(),
  keyPrefix: z.string(),
  isAdmin: z.boolean(),
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
