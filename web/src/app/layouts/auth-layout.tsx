import { Gauge, Server } from 'lucide-react';
import { Link, Outlet } from 'react-router';
import { Avatar } from '@/shared/ui/avatar';
import { CadenceWordmark } from '@/shared/ui/cadence-logo';
import { PriorityIcon, StatusIcon } from '@/shared/ui/work-glyphs';

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
      <BrandPanel />
    </div>
  );
}

/** Decorative: a glimpse of the product floating over the brand gradient. */
function BrandPanel() {
  return (
    <div
      data-theme="dark"
      aria-hidden="true"
      className="relative m-3 hidden flex-col justify-end overflow-hidden rounded-xl p-10 text-foreground lg:flex"
      style={{
        background:
          'radial-gradient(900px 420px at 15% -10%, rgb(45 194 174 / 0.35), transparent 60%), radial-gradient(700px 420px at 110% 120%, rgb(240 97 46 / 0.22), transparent 55%), linear-gradient(160deg, #0f2a2a, #0b0e14 55%)',
      }}
    >
      <FloatingCard className="top-14 left-14 w-64 -rotate-3">
        <div className="flex items-center gap-1.5">
          <PriorityIcon priority="high" />
          <span className="font-mono text-xs text-muted-foreground">ATL-142</span>
          <Avatar name="Grace Hopper" className="ml-auto size-5 text-[9px]" />
        </div>
        <p className="mt-1.5 font-medium">Offline sync drops edits made on the train</p>
      </FloatingCard>
      <FloatingCard className="top-32 right-12 w-56 rotate-2">
        <div className="flex items-center gap-2 font-medium">
          <StatusIcon category="done" />
          Sprint 13 shipped
        </div>
        <div className="mt-2.5 h-1.5 rounded-full bg-primary" />
        <p className="mt-1.5 text-xs text-muted-foreground">34 points · 2 days early</p>
      </FloatingCard>
      <FloatingCard className="top-56 left-28 w-60 -rotate-1">
        <div className="flex items-center gap-2">
          <Avatar name="Alan Turing" className="size-5 text-[9px]" />
          <span>
            Alan <span className="text-muted-foreground">reviewed</span>{' '}
            <span className="font-mono text-xs">#231</span>
          </span>
        </div>
      </FloatingCard>

      <div className="relative">
        <p className="max-w-sm text-3xl leading-tight font-semibold tracking-[-0.03em] text-white">
          Plan, track and ship <span className="text-[#67d5c3]">in rhythm</span>.
        </p>
        <ul className="mt-5 flex flex-col gap-2 text-sm text-white/75">
          <li className="flex items-center gap-2">
            <Server className="size-4 text-[#67d5c3]" />
            Self-hosted: your data stays on your server
          </li>
          <li className="flex items-center gap-2">
            <Gauge className="size-4 text-[#67d5c3]" />
            Fast on a 1 GB machine, live for the whole team
          </li>
        </ul>
      </div>
    </div>
  );
}

function FloatingCard({ className, children }: { className: string; children: React.ReactNode }) {
  return (
    <div
      className={`absolute rounded-lg border border-white/10 bg-[#151a23]/90 px-3 py-2.5 text-[13px] shadow-[0_20px_40px_rgb(0_0_0/0.4)] ${className}`}
    >
      {children}
    </div>
  );
}
