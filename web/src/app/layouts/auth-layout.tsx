import { lazy, Suspense } from 'react';
import { Link, Outlet } from 'react-router';
import { CadenceWordmark } from '@/shared/ui/cadence-logo';

// Decorative and hidden on small screens, so it loads after the form instead of with it.
const BrandPanel = lazy(() => import('./brand-panel'));

/** Account pages: the form on the left, the brand on the right on wide screens (design board 10). */
export function AuthLayout() {
  return (
    <div className="grid min-h-svh bg-background lg:grid-cols-[1fr_1.05fr]">
      <div className="flex flex-col px-6 py-8 sm:px-12 lg:px-16">
        <Link to="/" className="self-start text-xl" aria-label="Cadence home">
          <CadenceWordmark />
        </Link>
        <main className="mx-auto flex w-full max-w-[380px] flex-1 flex-col justify-center py-12">
          <Outlet />
        </main>
        <p className="text-xs text-muted-foreground">Self-hosted Cadence · MIT License</p>
      </div>
      <Suspense
        fallback={
          <div aria-hidden="true" className="m-3 hidden rounded-xl bg-[#0b0e14] lg:block" />
        }
      >
        <BrandPanel />
      </Suspense>
    </div>
  );
}
