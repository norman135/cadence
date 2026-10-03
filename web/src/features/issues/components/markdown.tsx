import { Suspense, use, useMemo } from 'react';
import { cn } from '@/shared/lib/utils';

const styles = cn(
  'max-w-[72ch] text-[15px] leading-6 break-words [&_a]:text-primary [&_a]:underline [&_a]:underline-offset-2',
  '[&_blockquote]:border-l-2 [&_blockquote]:pl-3 [&_blockquote]:text-muted-foreground',
  '[&_code]:rounded-[5px] [&_code]:border [&_code]:bg-sunken [&_code]:px-1 [&_code]:font-mono [&_code]:text-[0.86em]',
  '[&_pre]:my-3 [&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:border [&_pre]:bg-sunken [&_pre]:p-3 [&_pre_code]:border-0 [&_pre_code]:p-0',
  '[&_h1]:mt-4 [&_h1]:text-xl [&_h1]:font-semibold [&_h2]:mt-4 [&_h2]:text-lg [&_h2]:font-semibold [&_h3]:mt-3 [&_h3]:font-semibold',
  '[&_li]:my-0.5 [&_ol]:list-decimal [&_ol]:pl-6 [&_ul]:list-disc [&_ul]:pl-6',
  '[&_p]:my-2 [&>*:first-child]:mt-0 [&>*:last-child]:mb-0',
);

// marked and DOMPurify load on first use, so lists that never show Markdown don't download them.
let renderer: Promise<typeof import('../lib/render-markdown')> | undefined;
const loadRenderer = () => (renderer ??= import('../lib/render-markdown'));

/**
 * Renders Markdown written by users (descriptions, comments). The HTML is sanitized with DOMPurify
 * before it reaches the page, so a comment can't run scripts. Links open in a new tab. Until the
 * renderer has loaded, the source shows as plain text.
 */
export function Markdown({ source, className }: { source: string; className?: string }) {
  return (
    <Suspense
      fallback={<div className={cn(styles, 'whitespace-pre-wrap', className)}>{source}</div>}
    >
      <RenderedMarkdown source={source} className={className} />
    </Suspense>
  );
}

function RenderedMarkdown({
  source,
  className,
}: {
  source: string;
  className?: string | undefined;
}) {
  const { renderMarkdown } = use(loadRenderer());
  const html = useMemo(() => renderMarkdown(source), [renderMarkdown, source]);
  return <div className={cn(styles, className)} dangerouslySetInnerHTML={{ __html: html }} />;
}
