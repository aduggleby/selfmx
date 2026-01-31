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
