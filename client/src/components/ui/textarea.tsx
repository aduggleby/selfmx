import * as React from 'react';
import { cn } from '@/lib/utils';

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {}

const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ className, ...props }, ref) => {
    return (
      <textarea
        className={cn(
          'flex min-h-[100px] w-full rounded-2xl border border-input bg-background/80 px-4 py-3 text-base',
          'shadow-[inset_0_1px_2px_oklch(0%_0_0_/_0.05)]',
          'ring-offset-background placeholder:text-muted-foreground',
          'transition-all duration-200 ease-out resize-none',
          'hover:border-primary/60',
          'focus-visible:outline-none focus-visible:border-primary',
          'focus-visible:ring-2 focus-visible:ring-primary/20',
          'focus-visible:shadow-[inset_0_1px_2px_oklch(0%_0_0_/_0.05),_0_0_0_3px_oklch(0.62_0.18_185_/_0.15)]',
          'disabled:cursor-not-allowed disabled:opacity-50',
          className
        )}
        ref={ref}
        {...props}
      />
    );
  }
);
Textarea.displayName = 'Textarea';

export { Textarea };
