# feat: Stripe/Linear Visual Design Polish

## Overview

Elevate the Selfmx UI from a functional foundation to a polished, premium Stripe/Linear-inspired experience. The current implementation has the right architecture (Tailwind CSS 4 with OKLCH colors, dark mode support, semantic tokens) but lacks the visual depth, refined interactions, and micro-details that make Stripe and Linear's interfaces feel sophisticated.

## Problem Statement

The UI feels **bland** despite having correct fundamentals:
- **Flat appearance**: Cards and buttons lack visual depth (shadows are too subtle, no layering)
- **Static interactions**: Hover/focus states exist but feel mechanical (missing lift effects, glow)
- **Generic typography**: Inter font is loaded but not leveraged (tracking, weights need refinement)
- **Undifferentiated surfaces**: Dark mode uses similar shades, losing visual hierarchy
- **Missing micro-animations**: No entrance animations, transitions feel instant/jarring

## Proposed Solution

A comprehensive visual polish pass touching all UI components while maintaining the existing architecture:

1. **Enhanced shadow system** with multi-layer definitions and colored shadows
2. **Refined color tokens** with more surface gradation and subtle gradients
3. **Micro-interaction library** with standardized timing and easing
4. **Typography refinements** with proper tracking, weights, and hierarchy
5. **Component-level polish** for buttons, cards, inputs, badges, and tables

---

## Technical Approach

### Architecture

No architectural changes required. All improvements layer onto existing:
- CSS custom properties in `src/index.css`
- Tailwind utility classes in components
- Theme provider for dark/light mode

### Files to Modify

| File | Purpose | Changes |
|------|---------|---------|
| `src/index.css` | Design tokens | Add shadow tokens, gradients, animation keyframes |
| `src/components/ui/button.tsx` | Buttons | Enhanced hover/active states, colored shadows |
| `src/components/ui/card.tsx` | Cards | Hover lift, refined borders, layered shadows |
| `src/components/ui/input.tsx` | Inputs | Inner shadow, focus glow |
| `src/components/ui/skeleton.tsx` | Loading | Shimmer gradient animation |
| `src/components/DomainCard.tsx` | Domain cards | Entrance animation, DNS expand animation |
| `src/components/DomainStatusBadge.tsx` | Status badges | Ring insets, verifying pulse |
| `src/components/DnsRecordsTable.tsx` | DNS table | Row hover enhancement |
| `src/pages/DomainsPage.tsx` | Main page | Subtle background gradient |
| `src/App.tsx` | Layout | Header scroll shadow |

---

## Implementation Phases

### Phase 1: Design Token Foundation

Establish the enhanced token system in `src/index.css`.

#### Shadow Tokens

```css
/* src/index.css - Add after existing tokens */
@theme inline {
  /* Multi-layer shadows for depth */
  --shadow-elevation-low:
    0 1px 2px oklch(0% 0 0 / 0.04),
    0 1px 4px oklch(0% 0 0 / 0.04);
  --shadow-elevation-medium:
    0 2px 4px oklch(0% 0 0 / 0.04),
    0 4px 12px oklch(0% 0 0 / 0.08);
  --shadow-elevation-high:
    0 4px 8px oklch(0% 0 0 / 0.04),
    0 12px 32px oklch(0% 0 0 / 0.12);

  /* Colored shadows for brand elements */
  --shadow-primary-glow: 0 4px 14px oklch(0.55 0.24 263 / 0.25);
  --shadow-primary-glow-hover: 0 6px 20px oklch(0.55 0.24 263 / 0.35);

  /* Dark mode shadow adjustments */
  .dark {
    --shadow-elevation-low:
      0 1px 2px oklch(0% 0 0 / 0.2),
      0 0 0 1px oklch(100% 0 0 / 0.03);
    --shadow-elevation-medium:
      0 2px 8px oklch(0% 0 0 / 0.3),
      0 0 0 1px oklch(100% 0 0 / 0.05);
    --shadow-elevation-high:
      0 8px 24px oklch(0% 0 0 / 0.4),
      0 0 0 1px oklch(100% 0 0 / 0.05);
  }
}
```

#### Animation Keyframes

```css
/* src/index.css - Animation definitions */
@keyframes shimmer {
  from { background-position: -200% 0; }
  to { background-position: 200% 0; }
}

@keyframes fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes slide-up {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes scale-in {
  from {
    opacity: 0;
    transform: scale(0.96);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes pulse-ring {
  0%, 100% { opacity: 0.5; transform: scale(1); }
  50% { opacity: 0.2; transform: scale(1.05); }
}
```

#### Enhanced Surface Colors (Dark Mode)

```css
/* src/index.css - Improved dark mode surface gradation */
.dark {
  --background: oklch(0.12 0.015 260);      /* Darker base */
  --card: oklch(0.16 0.015 260);            /* More contrast from bg */
  --card-elevated: oklch(0.20 0.015 260);   /* For modals, popovers */
  --muted: oklch(0.14 0.01 260);
  --border: oklch(0.24 0.01 260);           /* More visible borders */
}
```

---

### Phase 2: Button Component Polish

#### Current State
```tsx
// src/components/ui/button.tsx - Current
className="hover:bg-primary/90"
```

#### Enhanced State
```tsx
// src/components/ui/button.tsx - Enhanced
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium transition-all duration-200 ease-out disabled:pointer-events-none disabled:opacity-50 motion-safe:active:scale-[0.98]",
  {
    variants: {
      variant: {
        default:
          "bg-primary text-primary-foreground shadow-[var(--shadow-elevation-low)] hover:bg-primary/90 hover:shadow-[var(--shadow-primary-glow)] focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
        destructive:
          "bg-destructive text-destructive-foreground shadow-[var(--shadow-elevation-low)] hover:bg-destructive/90 hover:shadow-[0_4px_14px_oklch(0.55_0.22_25_/_0.25)]",
        outline:
          "border border-border bg-background hover:bg-accent hover:text-accent-foreground hover:border-border/80",
        secondary:
          "bg-secondary text-secondary-foreground shadow-[var(--shadow-elevation-low)] hover:bg-secondary/80",
        ghost:
          "hover:bg-accent hover:text-accent-foreground",
        link:
          "text-primary underline-offset-4 hover:underline",
      },
    },
  }
);
```

---

### Phase 3: Card Component Enhancement

#### Current State
```tsx
// src/components/ui/card.tsx:9-11
className={cn(
  "rounded-xl border bg-card text-card-foreground shadow-md outline outline-1 -outline-offset-1 outline-black/5 dark:outline-white/5",
  className
)}
```

#### Enhanced State
```tsx
// src/components/ui/card.tsx - With hover lift
const Card = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        "rounded-xl border bg-card text-card-foreground",
        "shadow-[var(--shadow-elevation-medium)]",
        "outline outline-1 -outline-offset-1 outline-black/5 dark:outline-white/5",
        "transition-all duration-200 ease-out",
        "hover:shadow-[var(--shadow-elevation-high)] hover:-translate-y-0.5",
        className
      )}
      {...props}
    />
  )
);
```

---

### Phase 4: Input Component Refinement

```tsx
// src/components/ui/input.tsx - Enhanced focus state
const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, ...props }, ref) => {
    return (
      <input
        type={type}
        className={cn(
          "flex h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-base",
          "shadow-[inset_0_1px_2px_oklch(0%_0_0_/_0.05)]", // Inner shadow
          "placeholder:text-muted-foreground",
          "transition-all duration-200 ease-out",
          "hover:border-primary/50",
          "focus-visible:outline-none focus-visible:border-primary",
          "focus-visible:ring-2 focus-visible:ring-primary/20",
          "focus-visible:shadow-[inset_0_1px_2px_oklch(0%_0_0_/_0.05),_0_0_0_3px_oklch(0.55_0.24_263_/_0.1)]", // Focus glow
          "disabled:cursor-not-allowed disabled:opacity-50",
          className
        )}
        ref={ref}
        {...props}
      />
    );
  }
);
```

---

### Phase 5: Skeleton Shimmer Animation

```tsx
// src/components/ui/skeleton.tsx - With shimmer
function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        "rounded-md bg-muted",
        "bg-gradient-to-r from-muted via-muted/50 to-muted",
        "bg-[length:200%_100%]",
        "animate-[shimmer_1.5s_ease-in-out_infinite]",
        className
      )}
      {...props}
    />
  );
}
```

---

### Phase 6: Status Badge Enhancement

```tsx
// src/components/DomainStatusBadge.tsx - With ring insets and pulse
const statusStyles: Record<DomainStatus, string> = {
  pending: cn(
    "bg-[var(--status-pending-bg)] text-[var(--status-pending-text)]",
    "ring-1 ring-inset ring-[var(--status-pending-text)]/20"
  ),
  verifying: cn(
    "bg-[var(--status-verifying-bg)] text-[var(--status-verifying-text)]",
    "ring-1 ring-inset ring-[var(--status-verifying-text)]/20",
    "relative overflow-hidden"
  ),
  verified: cn(
    "bg-[var(--status-verified-bg)] text-[var(--status-verified-text)]",
    "ring-1 ring-inset ring-[var(--status-verified-text)]/20"
  ),
  failed: cn(
    "bg-[var(--status-failed-bg)] text-[var(--status-failed-text)]",
    "ring-1 ring-inset ring-[var(--status-failed-text)]/20"
  ),
};

// Add pulse indicator for verifying status
{status === 'verifying' && (
  <span className="absolute inset-0 rounded-full animate-[pulse-ring_2s_ease-in-out_infinite] bg-[var(--status-verifying-text)]/10" />
)}
```

---

### Phase 7: Domain Card Polish

```tsx
// src/components/DomainCard.tsx - Enhanced styling
<Card
  className={cn(
    "group",
    "animate-[scale-in_0.2s_ease-out]",
    "hover:shadow-[var(--shadow-elevation-high)] hover:shadow-blue-500/5",
    "transition-all duration-200 ease-out"
  )}
>
```

#### DNS Table Expand Animation

```tsx
// Wrap DNS table in animated container
<div
  className={cn(
    "grid transition-all duration-200 ease-out",
    showDnsRecords ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
  )}
>
  <div className="overflow-hidden">
    <DnsRecordsTable records={domain.dnsRecords} />
  </div>
</div>
```

---

### Phase 8: Page-Level Polish

#### Background Gradient

```tsx
// src/pages/DomainsPage.tsx - Subtle radial gradient
<div className="min-h-screen bg-gradient-to-b from-background via-background to-muted/30">
```

#### Header Scroll Shadow

```tsx
// src/App.tsx - Dynamic header shadow
const [scrolled, setScrolled] = useState(false);

useEffect(() => {
  const handleScroll = () => setScrolled(window.scrollY > 10);
  window.addEventListener('scroll', handleScroll);
  return () => window.removeEventListener('scroll', handleScroll);
}, []);

<header className={cn(
  "sticky top-0 z-50 border-b bg-background/80 backdrop-blur-sm transition-shadow duration-200",
  scrolled && "shadow-[var(--shadow-elevation-low)]"
)}>
```

---

## Acceptance Criteria

### Visual Design

- [x] Cards have visible lift effect on hover (transform + shadow change)
- [x] Buttons show colored glow shadows on hover/focus
- [x] Inputs have inner shadow and focus glow
- [x] Skeleton loaders animate with shimmer (not just pulse)
- [x] Status badges have ring insets for depth
- [x] "Verifying" badge has pulse animation
- [x] Dark mode has distinct surface levels (bg vs card vs elevated)
- [x] Page has subtle gradient background

### Interactions

- [x] All hover transitions use 200ms ease-out timing
- [x] Active button state scales to 0.98
- [x] DNS records table animates expand/collapse
- [x] New domain cards animate in (scale + fade)
- [x] Header gains shadow on scroll

### Accessibility

- [x] All animations respect `prefers-reduced-motion`
- [x] Focus states are distinct from hover states
- [x] Color contrast meets WCAG AA (4.5:1 for text)
- [x] Keyboard navigation shows visible focus rings

### Technical

- [x] No new dependencies added
- [x] Existing tests pass
- [x] Dark mode functions correctly
- [x] Performance: no layout shifts, smooth 60fps animations

---

## Testing Plan

1. **Visual regression**: Screenshot comparison before/after in light and dark modes
2. **Interaction testing**: Verify all hover/focus/active states work
3. **Animation testing**: Check transitions are smooth, not jarring
4. **Accessibility audit**: Run Lighthouse accessibility check
5. **Cross-browser**: Verify in Chrome, Firefox, Safari
6. **Responsive**: Test at mobile, tablet, desktop breakpoints

---

## References

### Internal Files

- Design tokens: `src/index.css:1-119`
- Button component: `src/components/ui/button.tsx:1-39`
- Card component: `src/components/ui/card.tsx:1-59`
- Input component: `src/components/ui/input.tsx:1-23`
- Domain card: `src/components/DomainCard.tsx:1-69`
- Status badge: `src/components/DomainStatusBadge.tsx:1-26`

### External Documentation

- [Tailwind CSS 4 Theme Configuration](https://tailwindcss.com/docs/theme)
- [Tailwind CSS Dark Mode](https://tailwindcss.com/docs/dark-mode)
- [Linear Design System](https://linear.style/)
- [Stripe Dashboard Design](https://stripe.com/blog/accessible-color-systems)
- [CSS Animations with prefers-reduced-motion](https://web.dev/prefers-reduced-motion/)

### Related Commits

- `2eea525` - feat(ui): polish UI with blue brand color and Tailwind UI style
- `a0d3538` - feat(ui): add Stripe/Linear inspired design system with dark mode
