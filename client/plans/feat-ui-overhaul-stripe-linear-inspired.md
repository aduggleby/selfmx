# feat: UI Overhaul - Stripe/Linear Inspired Design

## Overview

Transform the Selfmx dashboard from basic shadcn/ui defaults to a polished, Stripe/Linear inspired developer experience. Minimal, functional, but with visual refinement that makes it feel premium.

**Target aesthetic:** Clean monochromatic base with refined typography, subtle depth through shadows, and smooth micro-interactions.

## Problem Statement

Current UI is functional but generic:
- Default shadcn/ui colors lack personality
- No dark mode support
- "Loading..." text instead of elegant skeletons
- No feedback on user actions (toasts)
- Status badges use basic Tailwind colors without dark mode support

## Proposed Solution

### Visual System

**Color Palette (OKLCH for Tailwind v4):**
```css
/* Light mode - warm neutral base */
--background: oklch(0.99 0.002 240);     /* Slight warm tint */
--foreground: oklch(0.13 0.02 260);      /* Deep blue-gray */
--primary: oklch(0.45 0.2 260);          /* Indigo accent */
--muted: oklch(0.96 0.005 240);          /* Subtle gray */
--border: oklch(0.92 0.005 240);         /* Light border */

/* Dark mode - rich dark base */
--background: oklch(0.14 0.015 260);     /* Deep blue-black */
--foreground: oklch(0.95 0.01 240);      /* Off-white */
--primary: oklch(0.65 0.2 260);          /* Lighter indigo */
--border: oklch(0.25 0.01 260);          /* Subtle dark border */
```

**Typography:**
- System font stack with Inter as primary
- `tracking-tight` on headings
- `tabular-nums` for metrics/numbers
- Muted foreground for secondary text

**Spacing:**
- 8px grid system
- Consistent `gap-4` (16px) for card grids
- `p-6` (24px) for card content
- `space-y-8` (32px) for page sections

### Features

1. **Dark/Light Mode Toggle** - System preference detection with manual override
2. **Toast Notifications** - Sonner for action feedback
3. **Skeleton Loaders** - Elegant loading states
4. **Enhanced Cards** - Subtle shadows, hover states, refined borders
5. **Status Badges** - Dark mode compatible with softer colors
6. **Micro-animations** - Smooth transitions on interactive elements

## Technical Approach

### Phase 1: Theme Foundation

**Files to modify:**
- `src/index.css` - OKLCH color system, dark mode variables
- `src/App.tsx` - Add ThemeProvider wrapper
- `src/components/ui/theme-toggle.tsx` - New component

**CSS Variables Update (`src/index.css`):**
```css
@import "tailwindcss";

@custom-variant dark (&:is(.dark *));

:root {
  --radius: 0.625rem;
  --background: oklch(0.995 0.002 240);
  --foreground: oklch(0.13 0.02 260);
  --card: oklch(1 0 0);
  --card-foreground: oklch(0.13 0.02 260);
  --primary: oklch(0.45 0.2 260);
  --primary-foreground: oklch(0.98 0.005 260);
  --secondary: oklch(0.96 0.005 240);
  --secondary-foreground: oklch(0.13 0.02 260);
  --muted: oklch(0.96 0.005 240);
  --muted-foreground: oklch(0.5 0.02 260);
  --accent: oklch(0.96 0.01 260);
  --accent-foreground: oklch(0.13 0.02 260);
  --destructive: oklch(0.55 0.22 25);
  --destructive-foreground: oklch(0.98 0.005 0);
  --border: oklch(0.92 0.005 240);
  --input: oklch(0.92 0.005 240);
  --ring: oklch(0.45 0.2 260);

  /* Status colors */
  --status-pending-bg: oklch(0.95 0.08 85);
  --status-pending-text: oklch(0.45 0.12 85);
  --status-verifying-bg: oklch(0.93 0.06 250);
  --status-verifying-text: oklch(0.4 0.15 250);
  --status-verified-bg: oklch(0.93 0.08 155);
  --status-verified-text: oklch(0.35 0.15 155);
  --status-failed-bg: oklch(0.93 0.08 25);
  --status-failed-text: oklch(0.45 0.18 25);
}

.dark {
  --background: oklch(0.14 0.015 260);
  --foreground: oklch(0.95 0.01 240);
  --card: oklch(0.18 0.015 260);
  --card-foreground: oklch(0.95 0.01 240);
  --primary: oklch(0.7 0.18 260);
  --primary-foreground: oklch(0.13 0.02 260);
  --secondary: oklch(0.22 0.015 260);
  --secondary-foreground: oklch(0.95 0.01 240);
  --muted: oklch(0.22 0.015 260);
  --muted-foreground: oklch(0.65 0.02 260);
  --border: oklch(0.28 0.015 260);
  --input: oklch(0.22 0.015 260);
  --ring: oklch(0.6 0.18 260);

  /* Dark mode status colors */
  --status-pending-bg: oklch(0.28 0.06 85);
  --status-pending-text: oklch(0.8 0.1 85);
  --status-verifying-bg: oklch(0.25 0.05 250);
  --status-verifying-text: oklch(0.75 0.12 250);
  --status-verified-bg: oklch(0.25 0.06 155);
  --status-verified-text: oklch(0.8 0.12 155);
  --status-failed-bg: oklch(0.28 0.06 25);
  --status-failed-text: oklch(0.85 0.12 25);
}

@theme inline {
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-secondary: var(--secondary);
  --color-secondary-foreground: var(--secondary-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-destructive: var(--destructive);
  --color-destructive-foreground: var(--destructive-foreground);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --radius-sm: calc(var(--radius) - 4px);
  --radius-md: calc(var(--radius) - 2px);
  --radius-lg: var(--radius);
  --radius-xl: calc(var(--radius) + 4px);
}

@layer base {
  * {
    @apply border-border;
  }
  body {
    @apply bg-background text-foreground antialiased;
  }
}
```

### Phase 2: New Components

**Theme Provider (`src/components/theme-provider.tsx`):**
```tsx
import { createContext, useContext, useEffect, useState } from 'react'

type Theme = 'dark' | 'light' | 'system'

type ThemeProviderState = {
  theme: Theme
  setTheme: (theme: Theme) => void
}

const ThemeProviderContext = createContext<ThemeProviderState | undefined>(undefined)

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setTheme] = useState<Theme>(
    () => (localStorage.getItem('selfmx-theme') as Theme) || 'system'
  )

  useEffect(() => {
    const root = window.document.documentElement
    root.classList.remove('light', 'dark')

    if (theme === 'system') {
      const systemTheme = window.matchMedia('(prefers-color-scheme: dark)').matches
        ? 'dark'
        : 'light'
      root.classList.add(systemTheme)
      return
    }

    root.classList.add(theme)
  }, [theme])

  return (
    <ThemeProviderContext.Provider
      value={{
        theme,
        setTheme: (theme: Theme) => {
          localStorage.setItem('selfmx-theme', theme)
          setTheme(theme)
        },
      }}
    >
      {children}
    </ThemeProviderContext.Provider>
  )
}

export const useTheme = () => {
  const context = useContext(ThemeProviderContext)
  if (!context) throw new Error('useTheme must be used within ThemeProvider')
  return context
}
```

**Theme Toggle (`src/components/ui/theme-toggle.tsx`):**
```tsx
import { Moon, Sun } from 'lucide-react'
import { Button } from './button'
import { useTheme } from '../theme-provider'

export function ThemeToggle() {
  const { theme, setTheme } = useTheme()

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
      className="relative"
    >
      <Sun className="h-5 w-5 rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
      <Moon className="absolute h-5 w-5 rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
      <span className="sr-only">Toggle theme</span>
    </Button>
  )
}
```

**Skeleton (`src/components/ui/skeleton.tsx`):**
```tsx
import { cn } from '@/lib/utils'

export function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('animate-pulse rounded-md bg-muted', className)}
      {...props}
    />
  )
}
```

**Toaster Setup - Install Sonner:**
```bash
npm install sonner
```

**Toaster (`src/components/ui/toaster.tsx`):**
```tsx
import { useTheme } from '../theme-provider'
import { Toaster as Sonner } from 'sonner'

export function Toaster() {
  const { theme } = useTheme()

  return (
    <Sonner
      theme={theme === 'system' ? undefined : theme}
      className="toaster group"
      toastOptions={{
        classNames: {
          toast:
            'group toast group-[.toaster]:bg-card group-[.toaster]:text-foreground group-[.toaster]:border-border group-[.toaster]:shadow-lg',
          description: 'group-[.toast]:text-muted-foreground',
          actionButton: 'group-[.toast]:bg-primary group-[.toast]:text-primary-foreground',
          cancelButton: 'group-[.toast]:bg-muted group-[.toast]:text-muted-foreground',
        },
      }}
    />
  )
}
```

### Phase 3: Component Updates

**DomainStatusBadge (`src/components/DomainStatusBadge.tsx`):**
```tsx
import { cn } from '@/lib/utils'
import type { DomainStatus } from '@/lib/schemas'

const statusStyles: Record<DomainStatus, string> = {
  pending: 'bg-[var(--status-pending-bg)] text-[var(--status-pending-text)]',
  verifying: 'bg-[var(--status-verifying-bg)] text-[var(--status-verifying-text)]',
  verified: 'bg-[var(--status-verified-bg)] text-[var(--status-verified-text)]',
  failed: 'bg-[var(--status-failed-bg)] text-[var(--status-failed-text)]',
}

export function DomainStatusBadge({ status }: { status: DomainStatus }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium capitalize',
        statusStyles[status]
      )}
    >
      {status}
    </span>
  )
}
```

**DomainCard - Add shadows and transitions (`src/components/DomainCard.tsx`):**
- Add `shadow-sm hover:shadow-md transition-shadow duration-200` to Card
- Add subtle border styling

**DomainsPage - Add skeletons (`src/pages/DomainsPage.tsx`):**
```tsx
function DomainCardSkeleton() {
  return (
    <Card className="p-6">
      <div className="flex items-center justify-between mb-4">
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-5 w-16 rounded-full" />
      </div>
      <Skeleton className="h-4 w-48 mb-4" />
      <Skeleton className="h-9 w-20" />
    </Card>
  )
}
```

**Add toast feedback:**
```tsx
import { toast } from 'sonner'

// In handleCreate:
toast.success(`Domain ${newDomain.trim()} added`)

// In handleDelete:
toast.success('Domain deleted')

// On error:
toast.error(error.message)
```

### Phase 4: Header Enhancement

**Update App.tsx header:**
```tsx
<header className="sticky top-0 z-50 border-b bg-background/80 backdrop-blur-sm">
  <div className="container mx-auto flex items-center justify-between py-4 px-4">
    <h1 className="text-xl font-semibold tracking-tight">Selfmx</h1>
    <ThemeToggle />
  </div>
</header>
```

## Acceptance Criteria

- [ ] Dark/light mode toggle in header, persists to localStorage
- [ ] System preference detection on first visit
- [ ] Toast notifications on domain create/delete/error
- [ ] Skeleton loaders during initial page load
- [ ] Cards have subtle shadows with hover state
- [ ] Status badges work in both light and dark modes
- [ ] Smooth 200ms transitions on interactive elements
- [ ] All existing Playwright tests pass
- [ ] No layout shift during theme changes

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/index.css` | Modify - OKLCH color system |
| `src/components/theme-provider.tsx` | Create |
| `src/components/ui/theme-toggle.tsx` | Create |
| `src/components/ui/skeleton.tsx` | Create |
| `src/components/ui/toaster.tsx` | Create |
| `src/App.tsx` | Modify - Add providers, header toggle |
| `src/components/DomainStatusBadge.tsx` | Modify - CSS variable colors |
| `src/components/DomainCard.tsx` | Modify - Shadow, transitions |
| `src/pages/DomainsPage.tsx` | Modify - Skeletons, toasts |

## Dependencies

```bash
npm install sonner lucide-react
```

## References

- shadcn/ui Theming: https://ui.shadcn.com/docs/theming
- Sonner Toast: https://ui.shadcn.com/docs/components/sonner
- Tailwind CSS v4 Dark Mode: https://tailwindcss.com/docs/dark-mode
- OKLCH Color Format: https://oklch.com
