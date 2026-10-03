## Why

njord has comprehensive, accurate documentation (VitePress site with 12+ pages,
config builder, model catalog) and a functional README, but no visual identity.
There is no logo, no brand colors, no architecture diagrams, and the README is
plain text without badges or visual hierarchy. The repo looks like a work in
progress rather than a finished product — which it now is (all documented features
are implemented). A polished presentation builds trust for potential users
discovering the project on GitHub.

## What Changes

- **Logo**: Create an SVG logo with a Nordic-inspired compass/wind-rose motif
  (Njord = Norse god of wind and sea). Monochrome, works as favicon (48×48) and
  in navbar. Placed in `docs/public/logo.svg`.
- **VitePress theme**: Apply brand color palette (cool blues), add logo to navbar
  and hero section, customize CSS custom properties to replace default VitePress
  purple.
- **LikeC4 architecture diagrams**: Add a C4 architecture model
  (`docs/likec4/`) with views for the system overview, internal zones, and
  pipeline flow. Integrate via `@leberkas-org/vitepress-likec4` plugin
  (proven pattern from sibling projects). Embed diagrams in Getting Started and
  a new Architecture doc page.
- **README redesign**: Centered logo, badge row (build, license, Docker, .NET),
  feature highlights, minimal quick-start, architecture diagram (static SVG
  export from LikeC4), and link to full docs.
- **Docs content audit**: Remove references to features not yet exposed
  (consensus as a deferred feature per CLAUDE.md decisions), ensure enrichment
  docs match the actual implementation.

## Non-goals

- No new runtime features — this change is docs/branding only, zero code changes.
- No custom VitePress theme beyond color and logo — keep the default theme layout.
- No hand-drawn illustration or external design tooling — the logo is a
  programmatic SVG.
- No CI/CD changes (badge URLs will use shields.io static badges until CI is set up).

## Capabilities

### New Capabilities

- `branding`: Logo SVG, color palette, and VitePress theme customization.
- `architecture-diagrams`: LikeC4 C4 model, views, and VitePress plugin
  integration for interactive architecture diagrams.
- `readme`: GitHub-facing README with logo, badges, features, quick start, and
  architecture overview.

### Modified Capabilities

- `docs-site`: Add logo to navbar/hero, apply brand colors, add Architecture
  page, embed LikeC4 diagrams, audit content accuracy.

## Impact

- **docs/**: New `likec4/` directory with `.c4` model files, new
  `architecture.md` page, updated `index.md` hero, updated `.vitepress/config.ts`
  (LikeC4 plugin + logo + theme colors), new `public/logo.svg`.
- **docs/package.json**: New dependencies `likec4` and
  `@leberkas-org/vitepress-likec4`.
- **README.md**: Complete rewrite.
- **No source code changes** — `src/` is untouched.
