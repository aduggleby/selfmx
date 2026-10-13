/** Rolling send-count windows shown as table columns (API keys and domains). */
export const STAT_WINDOWS = [
  { key: 'last24h', label: '24h', title: 'Emails sent in the last 24 hours' },
  { key: 'last7d', label: '7d', title: 'Emails sent in the last 7 days' },
  { key: 'last30d', label: '30d', title: 'Emails sent in the last 30 days' },
  { key: 'last90d', label: '90d', title: 'Emails sent in the last 90 days' },
] as const;

export type StatKey = (typeof STAT_WINDOWS)[number]['key'];

export const STAT_UNAVAILABLE =
  'Not available: stats are loading or failed, or the window is longer than the sent-email retention.';

type WindowStats = { last24h: number; last7d: number | null; last30d: number | null; last90d: number | null };

/** null = unknown (not loaded, failed, or beyond retention); a missing entry with loaded stats = no sends. */
export function statValue(stats: WindowStats | undefined, key: StatKey, available: boolean): number | null {
  if (!available) return null;
  return stats ? stats[key] : 0;
}
