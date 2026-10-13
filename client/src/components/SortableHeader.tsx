import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';
import type { SortState } from '@/lib/sort';

/** Table header cell that sorts its column on click; plain text when no sort handler is given. */
export function SortableHeader<K extends string>({
  label,
  sortKey,
  sort,
  onSort,
  title,
  align = 'left',
}: {
  label: string;
  sortKey: K;
  sort?: SortState<K>;
  onSort?: (key: K) => void;
  title?: string;
  align?: 'left' | 'right';
}) {
  const active = sort?.key === sortKey;
  const Icon = !active ? ArrowUpDown : sort.dir === 'asc' ? ArrowUp : ArrowDown;
  return (
    <th
      className={`py-2 px-2 font-medium ${align === 'right' ? 'text-right' : ''}`}
      aria-sort={active ? (sort.dir === 'asc' ? 'ascending' : 'descending') : undefined}
      title={title}
    >
      {onSort ? (
        <button
          type="button"
          onClick={() => onSort(sortKey)}
          className={`inline-flex items-center gap-1 hover:text-foreground ${active ? 'text-foreground' : ''}`}
        >
          {label}
          <Icon className={`h-3 w-3 ${active ? '' : 'opacity-40'}`} />
        </button>
      ) : (
        label
      )}
    </th>
  );
}
