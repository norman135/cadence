import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PriorityIcon, StatusIcon } from './work-glyphs';

describe('work glyphs', () => {
  it('name the status for assistive technology', () => {
    render(<StatusIcon category="review" />);

    expect(screen.getByRole('img', { name: 'In review' })).toBeInTheDocument();
  });

  it('use a custom workflow state name when one is given', () => {
    render(<StatusIcon category="progress" label="Testing on device" />);

    expect(screen.getByRole('img', { name: 'Testing on device' })).toBeInTheDocument();
  });

  it('name the priority', () => {
    render(<PriorityIcon priority="urgent" />);

    expect(screen.getByRole('img', { name: 'Urgent' })).toBeInTheDocument();
  });
});
