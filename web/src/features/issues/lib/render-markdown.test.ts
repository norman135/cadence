import { describe, expect, it } from 'vitest';
import { renderMarkdown } from './render-markdown';

describe('renderMarkdown', () => {
  it('renders GitHub-flavored Markdown', () => {
    const html = renderMarkdown('**bold**\n\n- [x] done\n\n`code`');

    expect(html).toContain('<strong>bold</strong>');
    expect(html).toContain('type="checkbox"');
    expect(html).toContain('<code>code</code>');
  });

  it('strips scripts and event handlers', () => {
    const html = renderMarkdown('<script>alert(1)</script><img src="x" onerror="alert(1)">');

    expect(html).not.toContain('<script');
    expect(html).not.toContain('onerror');
  });

  it('drops javascript: links', () => {
    expect(renderMarkdown('[click](javascript:alert(1))')).not.toContain('javascript:');
  });

  it('opens links in a new tab without access to the page', () => {
    const html = renderMarkdown('[docs](https://example.com)');

    expect(html).toContain('target="_blank"');
    expect(html).toContain('rel="noreferrer noopener"');
  });
});
