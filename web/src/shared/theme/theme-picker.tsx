import { Monitor, Moon, Sun } from 'lucide-react';
import { cn } from '@/shared/lib/utils';
import { useTheme, type ThemePreference } from './theme';

const options: { value: ThemePreference; label: string; icon: typeof Sun }[] = [
  { value: 'system', label: 'System', icon: Monitor },
  { value: 'light', label: 'Light', icon: Sun },
  { value: 'dark', label: 'Dark', icon: Moon },
];

/** A segmented radio group for the theme; arrow keys move between options. */
export function ThemePicker({ className }: { className?: string }) {
  const { preference, setPreference } = useTheme();

  const move = (step: number) => {
    const index = options.findIndex((option) => option.value === preference);
    const next = options[(index + step + options.length) % options.length];
    if (next) setPreference(next.value);
  };

  return (
    <div
      role="radiogroup"
      aria-label="Theme"
      className={cn('inline-flex gap-0.5 rounded-[9px] border bg-sunken p-[3px]', className)}
      onKeyDown={(event) => {
        if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
          event.preventDefault();
          move(1);
        } else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
          event.preventDefault();
          move(-1);
        }
      }}
    >
      {options.map(({ value, label, icon: Icon }) => {
        const checked = preference === value;
        return (
          <button
            key={value}
            type="button"
            role="radio"
            aria-checked={checked}
            tabIndex={checked ? 0 : -1}
            onClick={() => {
              setPreference(value);
            }}
            className={cn(
              'inline-flex h-[26px] cursor-default items-center gap-1.5 rounded-md px-2.5 text-[13px] font-medium text-muted-foreground outline-none hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring',
              checked && 'bg-card text-foreground shadow-sm dark:bg-muted',
            )}
          >
            <Icon className="size-3.5" aria-hidden="true" />
            {label}
          </button>
        );
      })}
    </div>
  );
}
