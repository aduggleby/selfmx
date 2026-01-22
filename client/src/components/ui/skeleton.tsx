import { cn } from '@/lib/utils'

export function Skeleton({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        'rounded-md',
        'bg-gradient-to-r from-muted via-muted/50 to-muted',
        'bg-[length:200%_100%]',
        'motion-safe:animate-[shimmer_1.5s_ease-in-out_infinite]',
        'motion-reduce:bg-muted motion-reduce:animate-none',
        className
      )}
      {...props}
    />
  )
}
