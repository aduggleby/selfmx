# feat: UI Polish with Blue Brand Color and Tailwind UI Style

## Overview

Enhance the selfmx client UI from its current functional-but-bland state to a polished, premium feel inspired by Tailwind UI. This includes adopting a blue brand color, richer card styling, improved typography, and enhanced interactive states.

**Current State:** The app has a working OKLCH color system with dark mode, shadcn/ui components, and basic styling. However, it lacks visual punch and the refined details that make Tailwind UI designs feel premium.

**Target State:** A polished, professional interface with blue brand identity, subtle shadows, crisp typography, and satisfying micro-interactions.

---

## Technical Approach

### Color System Changes

Update the OKLCH color values to use a blue hue (~262) instead of the current purple-ish hue (~260). Add a blue color scale for colored shadows.

**Files:** `src/index.css`

### Typography Enhancements

Apply `tracking-tight` to headings and `tracking-wide` to uppercase labels/badges for improved readability and visual hierarchy.

**Files:** `src/components/ui/card.tsx`, `src/components/DomainStatusBadge.tsx`, `src/pages/DomainsPage.tsx`, `src/App.tsx`

### Card Styling

Add subtle outline borders (`outline outline-black/5`), enhanced shadows, and proper dark mode variants.

**Files:** `src/components/ui/card.tsx`, `src/components/DomainCard.tsx`

### Button Interactivity

Add `active:scale-[0.98]` for tactile feedback, improve transition properties, and ensure proper focus rings with the new blue color.

**Files:** `src/components/ui/button.tsx`

### Input Enhancement

Add hover states and improve focus styling with the new brand color.

**Files:** `src/components/ui/input.tsx`

### Table Polish

Add row hover states to the DNS records table.

**Files:** `src/components/DnsRecordsTable.tsx`

---

## Implementation Phases

### Phase 1: Color System Update

Update CSS variables to use blue hue and add blue color scale.

- [ ] Update `--primary` hue from 260 to 262 in `:root` (`src/index.css:13`)
- [ ] Update `--primary` in `.dark` to use blue hue (`src/index.css:47`)
- [ ] Update `--ring` to match new primary hue in both modes
- [ ] Update `--accent` hue to complement primary blue
- [ ] Add blue color scale variables for shadows:
  ```css
  --blue-400: oklch(0.70 0.17 255);
  --blue-500: oklch(0.62 0.21 260);
  --blue-600: oklch(0.55 0.24 263);
  ```
- [ ] Update `@theme inline` block to register blue colors

### Phase 2: Card Polish

Enhance card component with borders, shadows, and states.

- [ ] Update Card base styles in `src/components/ui/card.tsx`:
  ```tsx
  'rounded-xl border bg-card text-card-foreground shadow-md',
  'outline outline-1 outline-black/5 dark:outline-white/5',
  ```
- [ ] Update DomainCard hover effect (`src/components/DomainCard.tsx:19`):
  ```tsx
  'shadow-md hover:shadow-lg hover:shadow-blue-500/10 transition-all duration-200'
  ```

### Phase 3: Typography Refinement

Apply tracking utilities for crisp text.

- [ ] Update CardTitle tracking (`src/components/ui/card.tsx`):
  ```tsx
  'text-lg font-semibold leading-none tracking-tight'
  ```
- [ ] Update page heading (`src/pages/DomainsPage.tsx`):
  ```tsx
  'text-3xl font-bold tracking-tight mb-8'
  ```
- [ ] Update app title (`src/App.tsx`):
  ```tsx
  'text-xl font-semibold tracking-tight'
  ```
- [ ] Update DomainStatusBadge (`src/components/DomainStatusBadge.tsx`):
  ```tsx
  'uppercase text-xs font-semibold tracking-wide'
  ```

### Phase 4: Button Enhancement

Add active states and improve transitions.

- [ ] Update button base class (`src/components/ui/button.tsx:14`):
  ```tsx
  'transition-all duration-200 ease-out',
  'active:scale-[0.98]',
  ```
- [ ] Update primary variant hover:
  ```tsx
  'hover:bg-primary/90 hover:shadow-md hover:shadow-blue-500/20'
  ```
- [ ] Add colored focus ring:
  ```tsx
  'focus-visible:ring-blue-500'
  ```

### Phase 5: Input Styling

Add hover and improved focus states.

- [ ] Update Input component (`src/components/ui/input.tsx`):
  ```tsx
  'hover:border-primary/50',
  'focus-visible:ring-2 focus-visible:ring-blue-500/20 focus-visible:border-primary',
  ```

### Phase 6: Table Enhancement

Add row interactivity to DNS records table.

- [ ] Update DnsRecordsTable rows (`src/components/DnsRecordsTable.tsx`):
  ```tsx
  'hover:bg-muted/50 transition-colors'
  ```

### Phase 7: Final Polish

- [ ] Update skeleton loader tint if desired
- [ ] Update toast styling for consistency
- [ ] Run all Playwright tests to ensure nothing broke
- [ ] Manual visual review in both light and dark modes

---

## Acceptance Criteria

### Functional Requirements

- [ ] Blue primary color visible on all primary buttons
- [ ] Cards have subtle outline border visible in both modes
- [ ] Buttons have satisfying press feedback (scale effect)
- [ ] Focus rings are clearly visible with blue color
- [ ] Typography has crisp letter-spacing on headings
- [ ] Status badges have uppercase tracking
- [ ] Table rows highlight on hover

### Non-Functional Requirements

- [ ] All 41 existing Playwright tests pass
- [ ] Dark mode properly inverts all new styles
- [ ] No visual regressions in existing components
- [ ] Transitions feel smooth (not janky or sluggish)
- [ ] Focus states meet WCAG 2.1 visibility requirements

---

## Code Examples

### Updated index.css (key changes)

```css
:root {
  --radius: 0.625rem;

  /* Blue brand colors */
  --background: oklch(0.995 0.002 240);
  --foreground: oklch(0.13 0.02 260);
  --card: oklch(1 0 0);
  --card-foreground: oklch(0.13 0.02 260);

  --primary: oklch(0.55 0.24 263);           /* Blue-600 */
  --primary-foreground: oklch(0.99 0 0);

  --secondary: oklch(0.97 0.01 255);
  --secondary-foreground: oklch(0.25 0.05 260);

  --muted: oklch(0.96 0.005 240);
  --muted-foreground: oklch(0.45 0.02 260);

  --accent: oklch(0.96 0.02 255);
  --accent-foreground: oklch(0.25 0.05 260);

  --destructive: oklch(0.55 0.22 25);
  --destructive-foreground: oklch(0.99 0 0);

  --border: oklch(0.92 0.005 240);
  --input: oklch(0.92 0.005 240);
  --ring: oklch(0.55 0.24 263);

  /* Blue scale for shadows */
  --blue-400: oklch(0.70 0.17 255);
  --blue-500: oklch(0.62 0.21 260);
  --blue-600: oklch(0.55 0.24 263);

  /* Status colors unchanged */
  --status-pending-bg: oklch(0.95 0.08 85);
  --status-pending-text: oklch(0.45 0.12 85);
  /* ... rest of status colors ... */
}

.dark {
  --background: oklch(0.14 0.015 260);
  --foreground: oklch(0.96 0.005 240);
  --card: oklch(0.18 0.015 260);
  --card-foreground: oklch(0.96 0.005 240);

  --primary: oklch(0.70 0.17 255);           /* Blue-400 for dark */
  --primary-foreground: oklch(0.15 0.02 260);

  --ring: oklch(0.70 0.17 255);

  /* Blue scale for dark mode shadows */
  --blue-400: oklch(0.70 0.17 255);
  --blue-500: oklch(0.62 0.21 260);
  --blue-600: oklch(0.55 0.24 263);
}
```

### Updated Button Component

```tsx
// src/components/ui/button.tsx
const buttonVariants = cva(
  'inline-flex items-center justify-center whitespace-nowrap rounded-lg text-sm font-medium ' +
  'ring-offset-background transition-all duration-200 ease-out ' +
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ' +
  'disabled:pointer-events-none disabled:opacity-50 ' +
  'active:scale-[0.98]',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-primary/90 hover:shadow-md',
        destructive: 'bg-destructive text-destructive-foreground hover:bg-destructive/90',
        outline: 'border border-input bg-background hover:bg-accent hover:text-accent-foreground hover:border-primary/30',
        secondary: 'bg-secondary text-secondary-foreground hover:bg-secondary/80',
        ghost: 'hover:bg-accent hover:text-accent-foreground',
        link: 'text-primary underline-offset-4 hover:underline',
      },
      // ... sizes unchanged
    },
  }
);
```

### Updated Card Component

```tsx
// src/components/ui/card.tsx
const Card = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        'rounded-xl border bg-card text-card-foreground shadow-md',
        'outline outline-1 -outline-offset-1 outline-black/5 dark:outline-white/5',
        className
      )}
      {...props}
    />
  )
);
```

---

## References

### Internal Files
- `src/index.css:1-94` - Current color system
- `src/components/ui/button.tsx:4-39` - Current button variants
- `src/components/ui/card.tsx:4-15` - Current card styles
- `src/components/DomainStatusBadge.tsx` - Status badge styling
- `src/components/DomainCard.tsx:19` - Card hover effect

### External Resources
- [Tailwind CSS Colors](https://tailwindcss.com/docs/colors) - Blue palette reference
- [Tailwind CSS Box Shadow](https://tailwindcss.com/docs/box-shadow) - Shadow utilities
- [shadcn/ui Theming](https://ui.shadcn.com/docs/theming) - Color token structure
- [OKLCH Color Space](https://oklch.com/) - Color picker for OKLCH values

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Color contrast issues in dark mode | Medium | High | Test with contrast checker before finalizing |
| Existing tests break due to class changes | Low | Medium | Run test suite after each phase |
| Colored shadows look muddy in dark mode | Medium | Low | Use higher opacity or simpler shadows in dark mode |
| Active scale feels jarring | Low | Low | Use conservative 0.98 value, easily adjustable |

---

## Notes

- Keep status badge colors independent of brand color (they convey semantic meaning)
- Use `motion-safe:` prefix for scale effects to respect reduced motion preferences
- The blue hue shift from 260 to 263 is subtle but meaningful for a true blue feel
- Consider adding `shadow-blue-500/20` utility via CSS if Tailwind doesn't generate it automatically
