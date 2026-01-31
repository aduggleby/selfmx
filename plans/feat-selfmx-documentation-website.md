# feat: SelfMX Documentation Website

## Overview

Create a documentation website for SelfMX based on the Octoporty website design. SelfMX is a self-hosted, Resend-compatible email API powered by AWS SES with automatic domain verification via Cloudflare. The website will document the API, configuration, and usage patterns for developers self-hosting the service.

**Tech Stack:** Astro 5.x, Tailwind CSS 4.x, Shiki syntax highlighting
**Location:** `/home/alex/Source/selfmx/website`
**Brand Color:** Green (replacing Octoporty's red)
**Structure:** Multi-page with sidebar navigation

## Problem Statement / Motivation

SelfMX currently lacks dedicated documentation. The project has:
- A `CLAUDE.md` file with technical details (not user-facing)
- No README with getting started instructions
- No API reference documentation
- No configuration guide for self-hosters

A proper documentation site will:
1. Enable developers to self-host SelfMX
2. Provide API reference for integrations
3. Explain concepts like domain verification and multi-tenant API keys
4. Showcase Resend compatibility for migration

## Proposed Solution

### Architecture Overview

```
selfmx/website/
├── src/
│   ├── components/
│   │   ├── Header.astro          # Nav + theme toggle + mobile menu
│   │   ├── Footer.astro          # Links + copyright
│   │   ├── Sidebar.astro         # Documentation navigation
│   │   ├── CodeBlock.astro       # Syntax highlighting + copy button
│   │   ├── CodeTabs.astro        # Multi-language code examples
│   │   ├── ApiEndpoint.astro     # API endpoint display component
│   │   ├── ConfigTable.astro     # Environment variable tables
│   │   ├── TableOfContents.astro # Per-page TOC
│   │   └── SharedScripts.astro   # Theme toggle, copy, mobile menu
│   ├── layouts/
│   │   ├── BaseLayout.astro      # HTML wrapper, meta tags, fonts
│   │   ├── DocsLayout.astro      # Sidebar + content + TOC
│   │   └── ContentLayout.astro   # Markdown page wrapper
│   ├── pages/
│   │   ├── index.astro           # Landing page (hero, features, quick start)
│   │   ├── getting-started/
│   │   │   ├── index.astro       # Introduction
│   │   │   ├── quickstart.astro  # Quick start guide
│   │   │   └── requirements.astro# Self-hosting requirements
│   │   ├── configuration/
│   │   │   ├── index.astro       # Configuration overview
│   │   │   ├── aws-ses.astro     # AWS SES setup
│   │   │   └── cloudflare.astro  # Cloudflare integration
│   │   ├── api/
│   │   │   ├── index.astro       # API overview + authentication
│   │   │   ├── domains.astro     # Domains API reference
│   │   │   ├── emails.astro      # Emails API reference
│   │   │   ├── api-keys.astro    # API Keys management
│   │   │   └── audit-logs.astro  # Audit logs API
│   │   ├── concepts/
│   │   │   ├── domain-verification.astro
│   │   │   ├── multi-tenant-api-keys.astro
│   │   │   └── audit-logging.astro
│   │   ├── guides/
│   │   │   ├── sending-emails.astro
│   │   │   ├── setting-up-domains.astro
│   │   │   └── managing-api-keys.astro
│   │   ├── changelog.md
│   │   └── 404.astro
│   ├── lib/
│   │   └── highlighter.ts        # Shiki instance
│   ├── styles/
│   │   └── app.css               # Tailwind + custom theme
│   └── env.d.ts
├── public/
│   └── favicon.svg
├── astro.config.mjs
├── tsconfig.json
├── package.json
└── .prettierrc
```

### URL Structure

```
/                           # Landing page
/getting-started            # Introduction
/getting-started/quickstart # Quick start guide
/getting-started/requirements # Self-hosting requirements
/configuration              # Configuration overview
/configuration/aws-ses      # AWS SES setup
/configuration/cloudflare   # Cloudflare integration
/api                        # API overview + authentication
/api/domains                # Domains API
/api/emails                 # Emails API
/api/api-keys               # API Keys API
/api/audit-logs             # Audit Logs API
/concepts/domain-verification
/concepts/multi-tenant-api-keys
/concepts/audit-logging
/guides/sending-emails
/guides/setting-up-domains
/guides/managing-api-keys
/changelog
```

### Design System

**Brand Colors (Green):**
```css
--color-brand-50:  oklch(0.97 0.02 145);   /* Lightest green */
--color-brand-100: oklch(0.93 0.04 145);
--color-brand-200: oklch(0.86 0.08 145);
--color-brand-300: oklch(0.76 0.12 145);
--color-brand-400: oklch(0.64 0.16 145);
--color-brand-500: oklch(0.55 0.18 145);   /* Primary */
--color-brand-600: oklch(0.48 0.16 145);   /* Links, buttons */
--color-brand-700: oklch(0.40 0.14 145);
--color-brand-800: oklch(0.34 0.12 145);
--color-brand-900: oklch(0.28 0.10 145);   /* Darkest green */
```

**Typography:**
- Headings: Source Serif 4 (like Octoporty)
- Body: Inter sans-serif
- Code: JetBrains Mono or system monospace

**Dark Mode:**
- Class-based toggle: `.dark` on `<html>`
- Three modes: Light → Dark → System
- localStorage persistence

### Code Example Languages

Support for multi-language code examples:
- **cURL** - Universal HTTP examples
- **C#** - .NET developers (SelfMX is .NET)
- **JavaScript** - Node.js/frontend developers
- **Python** - Popular for scripting and automation

Configure Shiki with:
```javascript
langs: ["bash", "shell", "json", "yaml", "csharp", "javascript", "typescript", "python", "http"]
```

## Technical Approach

### Phase 1: Project Setup

1. **Initialize Astro project**
   ```bash
   cd /home/alex/Source/selfmx
   mkdir website && cd website
   npm create astro@latest . -- --template minimal --typescript strict
   ```

2. **Install dependencies**
   ```bash
   npm install tailwindcss @tailwindcss/vite @tailwindcss/typography shiki
   npm install -D prettier prettier-plugin-astro prettier-plugin-tailwindcss
   ```

3. **Configure Astro** (`astro.config.mjs`)
   - Static output mode
   - Shiki with dual themes (github-light/github-dark)
   - Tailwind via Vite plugin
   - Rehype plugin for external links

4. **Set up Tailwind** (`src/styles/app.css`)
   - Import Tailwind
   - Define green brand colors
   - Set up dark mode variant
   - Configure typography plugin

### Phase 2: Core Components

1. **BaseLayout.astro**
   - HTML boilerplate
   - Meta tags (title, description, OG, Twitter)
   - Font imports (Inter, Source Serif 4)
   - Theme initialization script (prevent FOUC)

2. **Header.astro**
   - SelfMX logo with green branding
   - Navigation: Getting Started, Configuration, API, Concepts, Guides
   - GitHub link
   - Theme toggle (sun/moon/system icons)
   - Mobile hamburger menu

3. **Sidebar.astro**
   - Collapsible navigation sections
   - Current page highlighting
   - Sticky positioning

4. **DocsLayout.astro**
   - Three-column layout: Sidebar | Content | TOC
   - Responsive collapse on mobile
   - Previous/Next page navigation

5. **CodeBlock.astro**
   - Shiki syntax highlighting
   - Optional filename label
   - Copy button with feedback
   - Dark theme by default

6. **CodeTabs.astro**
   - Tab switching for multiple languages
   - Persistent preference in localStorage
   - Smooth transitions

7. **ApiEndpoint.astro**
   - Method badge (GET, POST, DELETE)
   - Endpoint path display
   - Authentication indicator

8. **ConfigTable.astro**
   - Variable name, type, required, default, description
   - Dark mode compatible styling

### Phase 3: Landing Page

Create `src/pages/index.astro`:

1. **Hero Section**
   - Headline: "Self-hosted Email API"
   - Subheading: "Resend-compatible API powered by AWS SES"
   - Version badge
   - CTA buttons: Get Started, View on GitHub

2. **Features Grid**
   - Resend SDK Compatible
   - Automatic Domain Verification
   - Multi-tenant API Keys
   - Comprehensive Audit Trail
   - AWS SES Powered
   - Self-hosted Control

3. **Architecture Diagram**
   - ASCII or simple visual showing flow
   - Client → SelfMX API → AWS SES

4. **Quick Start Preview**
   - cURL example of sending an email
   - Link to full documentation

### Phase 4: Documentation Pages

1. **Getting Started**
   - Introduction to SelfMX
   - What it does, why self-host
   - Quick start with Docker
   - System requirements (Docker, AWS account, Cloudflare)

2. **Configuration**
   - Overview of all configuration options
   - AWS SES setup guide (IAM, identity verification)
   - Cloudflare integration (API token, zone setup)
   - Environment variables table

3. **API Reference**
   - Authentication (API keys, admin cookies)
   - Rate limiting (5/min login, 100/min API)
   - Each endpoint with:
     - Method + path
     - Description
     - Request parameters
     - Request body (with CodeTabs)
     - Response format
     - Error codes

4. **Concepts**
   - Domain Verification Flow (state machine diagram)
   - Multi-tenant API Keys (scoping, admin vs regular)
   - Audit Logging (what's logged, querying)

5. **Guides**
   - Step-by-step tutorials
   - Common use cases
   - Troubleshooting tips

### Phase 5: Polish & SEO

1. **SEO Configuration**
   - Sitemap generation
   - robots.txt
   - Open Graph images
   - Canonical URLs

2. **Accessibility**
   - Skip link
   - Keyboard navigation
   - Focus indicators
   - ARIA labels

3. **404 Page**
   - Helpful message
   - Search or navigation suggestions

4. **Performance**
   - Minimal JavaScript
   - Optimized fonts
   - Preload critical assets

## Acceptance Criteria

### Functional Requirements

- [ ] **Navigation**
  - [ ] Header with logo, nav links, GitHub link, theme toggle
  - [ ] Sidebar with collapsible sections
  - [ ] Per-page table of contents
  - [ ] Previous/Next page links
  - [ ] Mobile hamburger menu

- [ ] **Theme Toggle**
  - [ ] Three modes: Light, Dark, System
  - [ ] Persists preference in localStorage
  - [ ] No flash of wrong theme on load

- [ ] **Code Blocks**
  - [ ] Syntax highlighting for all specified languages
  - [ ] Copy button with "Copied!" feedback
  - [ ] Optional filename label
  - [ ] Multi-language tabs (CodeTabs component)

- [ ] **Content Pages**
  - [ ] All 5 main sections with subsections
  - [ ] API reference with all endpoints documented
  - [ ] Configuration tables with all env vars
  - [ ] Changelog page

- [ ] **Responsive Design**
  - [ ] Works on mobile, tablet, desktop
  - [ ] Sidebar collapses on mobile
  - [ ] TOC hidden on mobile
  - [ ] Touch-friendly navigation

### Non-Functional Requirements

- [ ] Lighthouse score > 90 for Performance, Accessibility, SEO
- [ ] No layout shift on page load
- [ ] Fast initial load (< 1s on good connection)
- [ ] Zero runtime JavaScript for static content

## Dependencies & Risks

### Dependencies
- Astro 5.x (stable)
- Tailwind CSS 4.x (stable)
- Shiki (syntax highlighting)
- No other runtime dependencies

### Risks
| Risk | Mitigation |
|------|------------|
| Content accuracy | Sync with CLAUDE.md and actual API |
| Design drift from Octoporty | Use Octoporty as direct reference |
| Dark mode complexity | Follow Octoporty's proven pattern |

## References

### Internal References
- Octoporty website: `/home/alex/Source/octoporty/website/`
- SelfMX CLAUDE.md: `/home/alex/Source/selfmx/CLAUDE.md`
- SelfMX Endpoints: `/home/alex/Source/selfmx/src/SelfMX.Api/Endpoints/`
- SelfMX Settings: `/home/alex/Source/selfmx/src/SelfMX.Api/Settings/AppSettings.cs`

### External References
- [Astro Documentation](https://docs.astro.build/)
- [Tailwind CSS 4 Documentation](https://tailwindcss.com/docs)
- [Shiki Documentation](https://shiki.style/)
- [Resend API Reference](https://resend.com/docs/api-reference) (compatibility target)

### Similar Projects
- [Octoporty Website](https://octoporty.dev) - Design reference
- [Resend Docs](https://resend.com/docs) - API documentation style reference
