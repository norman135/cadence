import { readdirSync } from 'node:fs';
import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/*
 * Module boundaries (feature-sliced):
 *   app/       the shell: providers, router, layouts. It may import a feature's public index
 *              (e.g. '@/features/home') or one of its route pages ('@/features/x/routes/page'),
 *              so the router can split every page into its own chunk. Nothing else inside.
 *   features/  one folder per feature. A feature may import itself and shared/, never
 *              another feature or the app shell.
 *   shared/    reusable building blocks. It may import only shared/.
 * Rules are generated per feature folder, so new features are covered automatically.
 */
const features = readdirSync(new URL('./src/features', import.meta.url), { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name);

const noDeepRelative = {
  regex: String.raw`^\.\./\.\./`,
  message: 'Use the @/ alias instead of climbing directories with ../../',
};
const noAppShell = {
  regex: '^@/app(/|$)',
  message: 'Only the app shell may import from @/app.',
};

const restrictImports = (...patterns) => ({
  'no-restricted-imports': ['error', { patterns: [noDeepRelative, ...patterns] }],
});

export default defineConfig([
  globalIgnores(['dist', 'coverage', 'src/shared/api/generated']),

  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.strictTypeChecked,
      tseslint.configs.stylisticTypeChecked,
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: restrictImports(),
  },

  {
    files: ['src/app/**/*.{ts,tsx}'],
    rules: restrictImports({
      regex: '^@/features/[^/]+/(?!routes/[^/]+$).+',
      message: "Import a feature through its public index or a route page, e.g. '@/features/home'.",
    }),
  },

  ...features.map((feature) => ({
    files: [`src/features/${feature}/**/*.{ts,tsx}`],
    rules: restrictImports(noAppShell, {
      regex: `^@/features/(?!${feature}(/|$))`,
      message: 'Features must not import other features. Move shared code to @/shared.',
    }),
  })),

  {
    files: ['src/shared/**/*.{ts,tsx}'],
    rules: restrictImports(noAppShell, {
      regex: '^@/features(/|$)',
      message: 'Shared code must not depend on features.',
    }),
  },

  {
    files: ['**/*.{js,mjs}'],
    extends: [js.configs.recommended],
    languageOptions: { globals: globals.node },
  },
]);
