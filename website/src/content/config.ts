// =============================================================================
// config.ts
//
// Summary: Content collection schemas for Astro.
//
// Defines the structure and validation for content collections:
// - pages: Documentation pages (e.g., install, api, concepts)
// =============================================================================

import { defineCollection, z } from "astro:content";

const pages = defineCollection({
  type: "content",
  schema: z.object({
    title: z.string(),
    description: z.string(),
    toc: z.boolean().optional(),
  }),
});

export const collections = { pages };
