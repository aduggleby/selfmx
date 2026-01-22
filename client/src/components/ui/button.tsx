import * as React from 'react';
import { cn } from '@/lib/utils';

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
  size?: 'default' | 'sm' | 'lg' | 'icon';
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = 'default', size = 'default', ...props }, ref) => {
    return (
      <button
        className={cn(
          'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium ring-offset-background transition-all duration-200 ease-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-50 motion-safe:active:scale-[0.98]',
          {
            'bg-primary text-primary-foreground shadow-[var(--shadow-elevation-low)] hover:bg-primary/90 hover:shadow-[var(--shadow-primary-glow)] focus-visible:ring-primary/50': variant === 'default',
            'bg-destructive text-destructive-foreground shadow-[var(--shadow-elevation-low)] hover:bg-destructive/90 hover:shadow-[var(--shadow-destructive-glow)] focus-visible:ring-destructive/50': variant === 'destructive',
            'border border-input bg-background hover:bg-accent hover:text-accent-foreground hover:border-border/80 focus-visible:ring-ring': variant === 'outline',
            'bg-secondary text-secondary-foreground shadow-[var(--shadow-elevation-low)] hover:bg-secondary/80 focus-visible:ring-ring': variant === 'secondary',
            'hover:bg-accent hover:text-accent-foreground focus-visible:ring-ring': variant === 'ghost',
            'text-primary underline-offset-4 hover:underline focus-visible:ring-ring': variant === 'link',
          },
          {
            'h-10 px-4 py-2': size === 'default',
            'h-9 rounded-md px-3': size === 'sm',
            'h-11 rounded-md px-8': size === 'lg',
            'h-10 w-10': size === 'icon',
          },
          className
        )}
        ref={ref}
        {...props}
      />
    );
  }
);
Button.displayName = 'Button';

export { Button };
