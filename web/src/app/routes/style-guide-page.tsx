import { Check, Copy, Filter, Kanban, List, Plus, Search, Sparkles, Trash2 } from 'lucide-react';
import { Alert } from '@/shared/ui/alert';
import { Avatar } from '@/shared/ui/avatar';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { CadenceWordmark } from '@/shared/ui/cadence-logo';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Checkbox } from '@/shared/ui/checkbox';
import { Input } from '@/shared/ui/input';
import { Kbd } from '@/shared/ui/kbd';
import { Label } from '@/shared/ui/label';
import { Switch } from '@/shared/ui/switch';
import { Tabs, TabsList, TabsTrigger } from '@/shared/ui/tabs';
import { TextField } from '@/shared/ui/text-field';
import {
  PriorityIcon,
  StatusIcon,
  type Priority,
  type WorkflowCategory,
} from '@/shared/ui/work-glyphs';

const statusNames: Record<WorkflowCategory, string> = {
  backlog: 'Backlog',
  todo: 'Todo',
  progress: 'In progress',
  review: 'In review',
  done: 'Done',
  canceled: 'Canceled',
};
const statuses = Object.keys(statusNames) as WorkflowCategory[];
const priorityNames: Record<Priority, string> = {
  urgent: 'Urgent',
  high: 'High',
  medium: 'Medium',
  low: 'Low',
  none: 'No priority',
};
const priorities = Object.keys(priorityNames) as Priority[];

/**
 * Every shared component in both themes, side by side (design board 05). Development builds
 * only: it is where components are checked against the designs, and what the visual regression
 * tests photograph.
 */
export function StyleGuidePage() {
  return (
    <div className="min-h-svh bg-canvas p-6">
      <header className="mb-6 flex items-center gap-3">
        <CadenceWordmark className="text-lg" />
        <span className="text-muted-foreground">Style guide</span>
      </header>
      <div className="grid gap-6 lg:grid-cols-2">
        <ThemePanel theme="light" />
        <ThemePanel theme="dark" />
      </div>
    </div>
  );
}

function ThemePanel({ theme }: { theme: 'light' | 'dark' }) {
  return (
    <section
      data-theme={theme}
      aria-label={`${theme} theme`}
      className="flex flex-col gap-7 rounded-xl border bg-background p-6 text-foreground"
    >
      <Group title="Buttons">
        <div className="flex flex-wrap items-center gap-2">
          <Button>
            <Plus />
            New issue
          </Button>
          <Button variant="secondary">Cancel</Button>
          <Button variant="soft">
            <Sparkles />
            Suggest
          </Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="destructive">Delete</Button>
          <Button variant="secondary" size="icon" aria-label="Copy link">
            <Copy />
          </Button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button disabled>Disabled</Button>
          <Button variant="secondary" size="sm">
            <Filter />
            Filter
          </Button>
          <Button size="lg">Sign in</Button>
        </div>
      </Group>

      <Group title="Inputs">
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="Title" defaultValue="Offline sync drops edits" />
          <TextField label="Email" defaultValue="grace@" error="Enter a valid email address." />
          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`${theme}-search`}>Search</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute top-2 left-2.5 size-4 text-subtle-foreground" />
              <Input id={`${theme}-search`} placeholder="Search issues…" className="pl-8" />
            </div>
          </div>
          <div className="flex items-end gap-5 pb-1.5">
            <label className="flex items-center gap-2">
              <Checkbox defaultChecked /> Checked
            </label>
            <label className="flex items-center gap-2">
              <Switch defaultChecked /> Notify me
            </label>
          </div>
        </div>
      </Group>

      <Group title="Badges and keys">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="primary">Owner</Badge>
          <Badge>Member</Badge>
          <Badge variant="success">
            <Check />
            Active
          </Badge>
          <Badge variant="warning">Due tomorrow</Badge>
          <Badge variant="destructive">Blocked</Badge>
          <Badge variant="info">Sprint 14</Badge>
          <Kbd>Ctrl</Kbd>
          <Kbd>K</Kbd>
        </div>
      </Group>

      <Group title="Status and priority">
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
          {statuses.map((category) => (
            <span key={category} className="flex items-center gap-2">
              <StatusIcon category={category} />
              <span>{statusNames[category]}</span>
            </span>
          ))}
        </div>
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
          {priorities.map((priority) => (
            <span key={priority} className="flex items-center gap-2">
              <PriorityIcon priority={priority} />
              {priorityNames[priority]}
            </span>
          ))}
        </div>
      </Group>

      <Group title="Avatars and tabs">
        <div className="flex flex-wrap items-center gap-6">
          <div className="flex items-center gap-2">
            <Avatar name="Ada Lovelace" className="size-8 text-xs" />
            <Avatar name="Alan Turing" />
            <Avatar name="Grace Hopper" />
            <Avatar name="Katherine Johnson" />
          </div>
          <Tabs defaultValue="list">
            <TabsList>
              <TabsTrigger value="list">
                <List />
                List
              </TabsTrigger>
              <TabsTrigger value="board">
                <Kanban />
                Board
              </TabsTrigger>
            </TabsList>
          </Tabs>
        </div>
      </Group>

      <Group title="Alerts and cards">
        <Alert variant="success">Your email is confirmed. You can sign in now.</Alert>
        <Alert variant="destructive">That link has expired. Ask Ada for a new one.</Alert>
        <Card>
          <CardHeader>
            <CardTitle>Delete Atlas Mobile?</CardTitle>
            <CardDescription>
              All 214 issues and their history will be deleted. This can&apos;t be undone.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex justify-end gap-2">
            <Button variant="secondary">Cancel</Button>
            <Button variant="destructive">
              <Trash2 />
              Delete project
            </Button>
          </CardContent>
        </Card>
      </Group>
    </section>
  );
}

function Group({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-3">
      <h2 className="font-mono text-[11px] tracking-[0.08em] text-muted-foreground uppercase">
        {title}
      </h2>
      {children}
    </div>
  );
}
