const pad = (n: number) => String(n).padStart(2, '0');

/** Absolute timestamp in the browser's time zone: YYYY-MM-DD HH:mm:ss (24h). */
export function formatDateTime(value: string | Date): string {
  const d = typeof value === 'string' ? new Date(value) : value;
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}
