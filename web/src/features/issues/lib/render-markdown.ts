import DOMPurify from 'dompurify';
import { marked } from 'marked';

marked.setOptions({ gfm: true, breaks: true });

// Links in user content open in a new tab without access to this page.
DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noreferrer noopener');
  }
});

/** Markdown to HTML that is safe to insert: DOMPurify strips scripts, handlers and unsafe URLs. */
export function renderMarkdown(source: string) {
  return DOMPurify.sanitize(marked.parse(source, { async: false }));
}
