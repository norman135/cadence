// Draws the product's small glyphs so the boards stay readable:
//   <span class="status" data-s="progress"></span>   workflow status
//   <span class="priority" data-p="high"></span>     priority
//   <span class="avatar" data-name="Ada Lovelace"></span>
//   <i data-lucide="search"></i>                       Lucide icons
// Sets window.boardReady once fonts and icons are in place (the PNG exporter waits for it).

const STATUS = {
  backlog: (c) =>
    `<circle cx="7" cy="7" r="5.5" fill="none" stroke="${c}" stroke-width="1.5" stroke-dasharray="2.2 1.9"/>`,
  todo: (c) => `<circle cx="7" cy="7" r="5.5" fill="none" stroke="${c}" stroke-width="1.5"/>`,
  progress: (c) =>
    `<circle cx="7" cy="7" r="5.5" fill="none" stroke="${c}" stroke-width="1.5"/><path d="M7 3.5 A3.5 3.5 0 0 1 7 10.5 Z" fill="${c}"/>`,
  review: (c) =>
    `<circle cx="7" cy="7" r="5.5" fill="none" stroke="${c}" stroke-width="1.5"/><path d="M7 3.5 A3.5 3.5 0 1 1 3.5 7 L7 7 Z" fill="${c}"/>`,
  done: (c) =>
    `<circle cx="7" cy="7" r="6.25" fill="${c}"/><path d="M4.4 7.1 6.2 8.9 9.7 5.3" fill="none" stroke="white" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>`,
  canceled: (c) =>
    `<circle cx="7" cy="7" r="6.25" fill="${c}"/><path d="M5 5 9 9M9 5 5 9" stroke="white" stroke-width="1.5" stroke-linecap="round"/>`,
};

const PRIORITY = {
  urgent: () =>
    `<rect x="0.5" y="0.5" width="13" height="13" rx="3.5" fill="var(--priority-urgent)"/><path d="M7 3.6v4.2" stroke="white" stroke-width="1.8" stroke-linecap="round"/><circle cx="7" cy="10.3" r="1.05" fill="white"/>`,
  high: () => bars(3, 'var(--text-muted)'),
  medium: () => bars(2, 'var(--text-muted)'),
  low: () => bars(1, 'var(--text-muted)'),
  none: () =>
    `<path d="M2 7h2M6 7h2M10 7h2" stroke="var(--text-subtle)" stroke-width="1.5" stroke-linecap="round"/>`,
};

function bars(filled, color) {
  return [0, 1, 2]
    .map((i) => {
      const h = 4 + i * 3;
      const on = i < filled;
      return `<rect x="${1.5 + i * 4}" y="${12 - h}" width="3" height="${h}" rx="1" fill="${color}" opacity="${on ? 1 : 0.25}"/>`;
    })
    .join('');
}

const AVATAR_COLORS = ['#0b7a70', '#3b6fd6', '#7c4ddb', '#c93f7d', '#c0431a', '#8f6a0a', '#1b7a44', '#556274'];

function hash(text) {
  let h = 0;
  for (const ch of text) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return h;
}

document.querySelectorAll('.status[data-s]').forEach((el) => {
  const s = el.dataset.s;
  el.innerHTML = `<svg viewBox="0 0 14 14" width="14" height="14">${STATUS[s](`var(--status-${s})`)}</svg>`;
});

document.querySelectorAll('.priority[data-p]').forEach((el) => {
  el.innerHTML = `<svg viewBox="0 0 14 14" width="14" height="14">${PRIORITY[el.dataset.p]()}</svg>`;
});

document.querySelectorAll('.avatar[data-name]').forEach((el) => {
  const name = el.dataset.name;
  el.textContent = name
    .split(' ')
    .map((part) => part[0])
    .slice(0, 2)
    .join('');
  el.style.background = AVATAR_COLORS[hash(name) % AVATAR_COLORS.length];
  el.title = name;
});

if (window.lucide) window.lucide.createIcons({ attrs: { 'stroke-width': 1.75 } });

document.fonts.ready.then(() => {
  window.boardReady = true;
});
