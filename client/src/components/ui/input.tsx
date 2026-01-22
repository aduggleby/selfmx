import * as React from 'react';
import { cn } from '@/lib/utils';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, ...props }, ref) => {
    return (
      <input
        type={type}
        className={cn(
          'flex h-11 w-full rounded-2xl border border-input bg-background/80 px-4 py-2 text-base',
          'shadow-[inset_0_1px_2px_oklch(0%_0_0_/_0.05)]',
          'ring-offset-background placeholder:text-muted-foreground',
          'transition-all duration-200 ease-out',
          'file:border-0 file:bg-transparent file:text-sm file:font-medium',
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
Input.displayName = 'Input';

export { Input };
