import { ArrowRight, FolderKanban, Palette, UserPlus, Zap, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';
import { Badge } from '@/shared/ui/badge';
import { CadenceGlyph } from '@/shared/ui/cadence-logo';
import { can, useCurrentOrganization, useCurrentUser } from '@/shared/workspace';

function greeting(hour: number) {
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}

/**
 * My work (design board 06): where every day starts. Until issues exist (M3), "Needs you" is
 * empty and the page points at the next steps.
 */
export function OrganizationHomePage() {
  const { organization } = useCurrentOrganization();
  const { data: me } = useCurrentUser();
  if (!organization || !me) return null;

  const now = new Date();
  const firstName = me.displayName.split(/\s+/)[0] ?? me.displayName;
  const base = `/${organization.slug}`;

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-8">
      <div>
        <p className="text-[13px] text-muted-foreground">
          {now.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}
        </p>
        <h1 className="mt-0.5 text-[30px] leading-tight font-semibold tracking-[-0.025em]">
          {greeting(now.getHours())}, {firstName}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Nothing needs you yet. Here&apos;s how to get {organization.name} going.
        </p>
      </div>

      <section aria-labelledby="needs-you" className="flex flex-col gap-2">
        <h2 id="needs-you" className="flex items-center gap-2 font-semibold tracking-[-0.01em]">
          <Zap className="size-4 text-brand-accent" aria-hidden="true" />
          Needs you
        </h2>
        <div className="flex flex-col items-center gap-2 rounded-lg border px-6 py-10 text-center">
          <span className="mb-1 grid size-12 place-items-center rounded-xl bg-primary-soft text-primary">
            <CadenceGlyph className="size-8" />
          </span>
          <p className="font-semibold">You&apos;re all caught up</p>
          <p className="max-w-sm text-sm text-muted-foreground">
            Reviews, mentions and blockers land here, in the order they need you.
          </p>
        </div>
      </section>

      <section aria-labelledby="get-started" className="flex flex-col gap-2">
        <h2 id="get-started" className="font-semibold tracking-[-0.01em]">
          Get started
        </h2>
        <ul className="divide-y overflow-hidden rounded-lg border">
          {can(organization.role, 'members.invite') && (
            <Step
              icon={UserPlus}
              title="Invite your team"
              description="Add people by email and choose what they can do."
              to={`${base}/settings/members`}
            />
          )}
          <Step
            icon={FolderKanban}
            title="Create a project"
            description="Projects, issues and boards arrive in the next release."
            badge="Soon"
          />
          <Step
            icon={Palette}
            title="Pick your theme"
            description="Light, dark, or follow your system."
            to={`${base}/settings/profile`}
          />
        </ul>
      </section>
    </div>
  );
}

function Step({
  icon: Icon,
  title,
  description,
  to,
  badge,
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  to?: string;
  badge?: string;
}) {
  const content = (
    <>
      <span className="grid size-8 shrink-0 place-items-center rounded-md border bg-sunken text-muted-foreground">
        <Icon className="size-4" aria-hidden="true" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block font-medium">{title}</span>
        <span className="block text-[13px] text-muted-foreground">{description}</span>
      </span>
      {badge && <Badge>{badge}</Badge>}
      {to && <ArrowRight className="size-4 text-subtle-foreground" aria-hidden="true" />}
    </>
  );

  return (
    <li>
      {to ? (
        <Link
          to={to}
          className="flex items-center gap-3 px-4 py-3 outline-none hover:bg-accent/60 focus-visible:bg-accent"
        >
          {content}
        </Link>
      ) : (
        <div className="flex items-center gap-3 px-4 py-3">{content}</div>
      )}
    </li>
  );
}
