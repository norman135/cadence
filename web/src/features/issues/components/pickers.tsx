import { Gauge, Tag, UserRound } from 'lucide-react';
import type { ReactNode } from 'react';
import type {
  IssuePriority,
  IssueStatus,
  IssueType,
  LabelResponse,
} from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { Avatar } from '@/shared/ui/avatar';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { PriorityIcon, StatusIcon } from '@/shared/ui/work-glyphs';
import { useLabels, useProjectPeople } from '@/shared/workspace';
import {
  ESTIMATES,
  LABEL_COLORS,
  PRIORITIES,
  STATUSES,
  TYPES,
  priorityOf,
  statusOf,
} from '../lib/options';

/*
 * One-click pickers for issue properties (design board 08's side panel). Each renders its current
 * value as a quiet button that opens a menu; read-only callers pass disabled.
 */

const trigger =
  'flex h-7 min-w-0 cursor-default items-center gap-2 rounded-md px-1.5 -mx-1.5 text-[13px] outline-none hover:bg-accent focus-visible:outline-2 focus-visible:outline-ring disabled:pointer-events-none data-[state=open]:bg-accent';

function Picker({
  label,
  value,
  disabled,
  children,
  className,
}: {
  label: string;
  value: ReactNode;
  disabled?: boolean | undefined;
  children: ReactNode;
  className?: string;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label={label}
        disabled={disabled}
        className={cn(trigger, className)}
      >
        {value}
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="max-h-80 overflow-y-auto">
        <DropdownMenuLabel>{label}</DropdownMenuLabel>
        {children}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export function StatusPicker({
  value,
  onChange,
  disabled,
  compact,
}: {
  value: IssueStatus;
  onChange: (value: IssueStatus) => void;
  disabled?: boolean;
  compact?: boolean;
}) {
  const current = statusOf(value);
  return (
    <Picker
      label="Status"
      disabled={disabled}
      value={
        <>
          <StatusIcon category={current.glyph} />
          {!compact && <span className="truncate">{current.label}</span>}
        </>
      }
    >
      <DropdownMenuRadioGroup
        value={value}
        onValueChange={(next) => {
          onChange(next as IssueStatus);
        }}
      >
        {STATUSES.map((status) => (
          <DropdownMenuRadioItem key={status.value} value={status.value}>
            <StatusIcon category={status.glyph} />
            {status.label}
          </DropdownMenuRadioItem>
        ))}
      </DropdownMenuRadioGroup>
    </Picker>
  );
}

export function PriorityPicker({
  value,
  onChange,
  disabled,
  compact,
}: {
  value: IssuePriority;
  onChange: (value: IssuePriority) => void;
  disabled?: boolean;
  compact?: boolean;
}) {
  const current = priorityOf(value);
  return (
    <Picker
      label="Priority"
      disabled={disabled}
      value={
        <>
          <PriorityIcon priority={current.glyph} />
          {!compact && <span className="truncate">{current.label}</span>}
        </>
      }
    >
      <DropdownMenuRadioGroup
        value={value}
        onValueChange={(next) => {
          onChange(next as IssuePriority);
        }}
      >
        {PRIORITIES.map((priority) => (
          <DropdownMenuRadioItem key={priority.value} value={priority.value}>
            <PriorityIcon priority={priority.glyph} />
            {priority.label}
          </DropdownMenuRadioItem>
        ))}
      </DropdownMenuRadioGroup>
    </Picker>
  );
}

export function TypePicker({
  value,
  onChange,
  disabled,
}: {
  value: IssueType;
  onChange: (value: IssueType) => void;
  disabled?: boolean;
}) {
  return (
    <Picker label="Type" disabled={disabled} value={<span>{value}</span>}>
      <DropdownMenuRadioGroup
        value={value}
        onValueChange={(next) => {
          onChange(next as IssueType);
        }}
      >
        {TYPES.map((type) => (
          <DropdownMenuRadioItem key={type.value} value={type.value}>
            {type.label}
          </DropdownMenuRadioItem>
        ))}
      </DropdownMenuRadioGroup>
    </Picker>
  );
}

const UNASSIGNED = 'none';

export function AssigneePicker({
  projectId,
  value,
  onChange,
  disabled,
}: {
  projectId: string;
  value: { id: string; displayName: string } | null | undefined;
  onChange: (userId: string | null) => void;
  disabled?: boolean;
}) {
  const { data: people } = useProjectPeople(projectId);
  return (
    <Picker
      label="Assignee"
      disabled={disabled}
      value={
        value ? (
          <>
            <Avatar name={value.displayName} className="size-5 text-[9px]" />
            <span className="truncate">{value.displayName}</span>
          </>
        ) : (
          <>
            <UserRound className="size-4 text-subtle-foreground" />
            <span className="text-muted-foreground">Unassigned</span>
          </>
        )
      }
    >
      <DropdownMenuRadioGroup
        value={value?.id ?? UNASSIGNED}
        onValueChange={(next) => {
          onChange(next === UNASSIGNED ? null : next);
        }}
      >
        <DropdownMenuRadioItem value={UNASSIGNED}>
          <UserRound />
          Unassigned
        </DropdownMenuRadioItem>
        {people?.map((person) => (
          <DropdownMenuRadioItem key={person.userId} value={person.userId}>
            <Avatar name={person.displayName} className="size-5 text-[9px]" />
            {person.displayName}
          </DropdownMenuRadioItem>
        ))}
      </DropdownMenuRadioGroup>
    </Picker>
  );
}

export function LabelChip({ label }: { label: LabelResponse }) {
  return (
    <span className="inline-flex h-5 items-center gap-1.5 rounded-full border px-2 text-[11.5px] font-medium whitespace-nowrap text-muted-foreground">
      <i
        aria-hidden="true"
        className="size-1.5 rounded-full"
        style={{ backgroundColor: LABEL_COLORS[label.color] }}
      />
      {label.name}
    </span>
  );
}

export function LabelsPicker({
  value,
  onChange,
  disabled,
}: {
  value: readonly LabelResponse[];
  onChange: (labelIds: string[]) => void;
  disabled?: boolean;
}) {
  const { data: labels } = useLabels();
  const selected = new Set(value.map((label) => label.id));

  return (
    <Picker
      label="Labels"
      disabled={disabled}
      className="h-auto min-h-7 flex-wrap py-1"
      value={
        value.length > 0 ? (
          value.map((label) => <LabelChip key={label.id} label={label} />)
        ) : (
          <>
            <Tag className="size-4 text-subtle-foreground" />
            <span className="text-muted-foreground">Add labels</span>
          </>
        )
      }
    >
      {labels?.length === 0 && (
        <p className="px-2 py-1.5 text-[13px] text-muted-foreground">No labels yet.</p>
      )}
      {labels?.map((label) => (
        <DropdownMenuCheckboxItem
          key={label.id}
          checked={selected.has(label.id)}
          onCheckedChange={(checked) => {
            const next = new Set(selected);
            if (checked) next.add(label.id);
            else next.delete(label.id);
            onChange([...next]);
          }}
        >
          <i
            aria-hidden="true"
            className="size-2 rounded-full"
            style={{ backgroundColor: LABEL_COLORS[label.color] }}
          />
          {label.name}
        </DropdownMenuCheckboxItem>
      ))}
    </Picker>
  );
}

export function EstimatePicker({
  value,
  onChange,
  disabled,
}: {
  value: number | null | undefined;
  onChange: (points: number | null) => void;
  disabled?: boolean;
}) {
  return (
    <Picker
      label="Estimate"
      disabled={disabled}
      value={
        value != null ? (
          <span>
            <span className="font-mono">{value}</span> points
          </span>
        ) : (
          <>
            <Gauge className="size-4 text-subtle-foreground" />
            <span className="text-muted-foreground">No estimate</span>
          </>
        )
      }
    >
      <DropdownMenuRadioGroup
        value={value != null ? String(value) : 'none'}
        onValueChange={(next) => {
          onChange(next === 'none' ? null : Number(next));
        }}
      >
        <DropdownMenuRadioItem value="none">No estimate</DropdownMenuRadioItem>
        {ESTIMATES.map((points) => (
          <DropdownMenuRadioItem key={points} value={String(points)}>
            <span className="font-mono">{points}</span> points
          </DropdownMenuRadioItem>
        ))}
      </DropdownMenuRadioGroup>
    </Picker>
  );
}
