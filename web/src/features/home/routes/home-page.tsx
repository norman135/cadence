import { ApiStatusCard } from '../components/api-status-card';

export function HomePage() {
  return (
    <div className="flex flex-col gap-10">
      <section className="flex flex-col gap-3">
        <p className="text-sm font-semibold text-primary">Milestone M0 · Foundation</p>
        <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">
          Project management with a steady rhythm.
        </h1>
        <p className="max-w-2xl text-lg text-muted-foreground">
          Cadence helps teams plan sprints, move work across real-time boards, and ship predictably.
          It is self-hosted and fast on small hardware.
        </p>
      </section>

      <ApiStatusCard />
    </div>
  );
}
