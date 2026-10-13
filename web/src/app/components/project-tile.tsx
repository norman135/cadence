const COLORS = [
  '#139f8f',
  '#3b6fd6',
  '#7c4ddb',
  '#c93f7d',
  '#c0431a',
  '#8f6a0a',
  '#1b7a44',
  '#556274',
];

/** A project's tile: its key's first letter on a color derived from the key (design board 06). */
export function ProjectTile({ projectKey }: { projectKey: string }) {
  let hash = 0;
  for (const character of projectKey) hash = (hash * 31 + character.charCodeAt(0)) >>> 0;

  return (
    <span
      aria-hidden="true"
      className="grid size-4 shrink-0 place-items-center rounded-[5px] text-[9px] font-bold text-white"
      style={{ backgroundColor: COLORS[hash % COLORS.length] }}
    >
      {projectKey.charAt(0)}
    </span>
  );
}
