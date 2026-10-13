export type SortDirection = 'asc' | 'desc';
export type SortState<K extends string> = { key: K; dir: SortDirection };

export const byText = (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: 'base', numeric: true });
export const byNumber = (a: number, b: number) => a - b;

/** Compares two possibly-missing values; missing values always sort last, in either direction. */
export function compareNullable<T>(a: T | null, b: T | null, compare: (x: T, y: T) => number, dir: 1 | -1): number {
  if (a === null || b === null) return a === b ? 0 : a === null ? 1 : -1;
  return compare(a, b) * dir;
}

/**
 * Next sort state after clicking a column: the same column flips direction; a new column
 * starts A→Z for text and newest/highest first for everything else.
 */
export function nextSort<K extends string>(current: SortState<K>, key: K, textKeys: readonly K[]): SortState<K> {
  if (current.key === key) return { key, dir: current.dir === 'asc' ? 'desc' : 'asc' };
  return { key, dir: textKeys.includes(key) ? 'asc' : 'desc' };
}
