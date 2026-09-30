# 0016. Brand and design system

- Status: Accepted
- Date: 2026-09-30

## Context

Through M1, the web app used the neutral shadcn/ui defaults: correct and accessible, but anonymous. The feature milestones (projects, boards, sprints, reporting) will add dozens of screens. Designing each one while building it leads to drift, rework and a product that looks assembled rather than designed.

The design system also has to respect the project's constraints:

- **Performance.** Brand assets count against the same budgets as code: 180 KB of initial JavaScript, 30 KB of initial CSS, and a fast first paint on a small server.
- **Self-hosting.** A Cadence server may run on a network without internet access, so nothing can load from third-party CDNs at runtime.
- **Accessibility.** Text must meet WCAG 2.2 AA in both themes, and status must not rely on color alone.
- **Open source.** Fonts and icons must allow redistribution under the MIT-licensed repository.

## Decision

**Design first, as its own milestone (M2).** The identity, tokens, components and key screens are designed up front, and the app is then rebuilt to match. Later milestones implement screens from these boards instead of inventing them.

**The source of truth lives in the repository, as code:**

- `design/tokens.css` defines every color, type size, radius, shadow and motion value in three layers: palette, semantic tokens (redefined for dark mode) and domain tokens (status, priority, labels). The web app's Tailwind theme maps onto the semantic layer, so components never reference palette colors.
- `design/boards/*.html` are the design boards, built from those tokens with the same component vocabulary as the app. `design/render.mjs` renders them to `design/exports/*.png` with the Playwright copy the web app already uses for end-to-end tests. Designs are reviewed in pull requests like code, and there is no proprietary design file to go stale.

**Identity:**

- **Tempo** (a deep teal) is the brand and action color. It is distinct from the blues and violets of other trackers, and its 600 step carries white text at 5.2:1.
- **Ember** (orange) is the accent: the "beat" in the logo, highlights and charts. It never carries body text.
- **Ink** is a cool neutral scale for text, surfaces and borders.
- The mark is three rising rounded bars on a Tempo tile, with the last bar in Ember. It stays legible at 16 px.

**Type:** Geist for the UI and Geist Mono for issue keys, code and tabular figures. Both are SIL OFL fonts, self-hosted as variable WOFF2 files with a Latin subset (29 KB and 23 KB). The UI base size is 14 px; long-form text is 16 px.

**Icons:** Lucide (ISC), imported per icon so unused icons are tree-shaken. Workflow status and priority use custom 14 px glyphs whose shape carries the meaning, so they read in grayscale and for color-blind users.

**Themes:** light and dark are equal citizens. The theme follows the operating system by default, and each user can override it.

## Consequences

- Feature milestones start from finished designs. The cost is one milestone with little new functionality.
- Every screen change can be checked against a board, and M2 adds visual regression snapshots to keep the app and the designs from drifting apart.
- The boards are HTML, so they can only show what the component vocabulary can express. That is intentional: a design that can't be built from the system is a prompt to extend the system first.
- Rendering the boards needs internet access for their font and icon CDNs. The app itself never does, because it ships its own copies.
- Changing a brand color is a one-line token change followed by a re-render. Changing a component is a change in both `shared/ui` and the boards' stylesheet, which review has to keep in step.
