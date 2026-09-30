// Shared sidebar for the product screens: <nav class="sidebar" data-active="board"></nav>
// Runs before board.js, which then draws the icons and avatars inside it.

const PROJECTS = [
  { key: 'ATL', name: 'Atlas Mobile', color: 'var(--tempo-500)', open: true },
  { key: 'NIM', name: 'Nimbus API', color: 'var(--blue-500)' },
  { key: 'WEB', name: 'Website', color: 'var(--violet-500)' },
];

function navItem(id, icon, text, active, extra = '') {
  return `<div class="nav-item${id === active ? ' active' : ''}"><i data-lucide="${icon}"></i>${text}${extra}</div>`;
}

document.querySelectorAll('nav.sidebar[data-active]').forEach((nav) => {
  const active = nav.dataset.active;
  const projects = PROJECTS.map((project) => {
    const head = `<div class="nav-item"><span class="project-dot" style="background:${project.color}">${project.key[0]}</span>${project.name}<i data-lucide="${project.open ? 'chevron-down' : 'chevron-right'}" style="margin-left:auto;width:14px;height:14px"></i></div>`;
    if (!project.open) return head;
    const sub = [
      ['issues', 'circle-dot', 'Issues'],
      ['board', 'kanban', 'Board'],
      ['backlog', 'list-todo', 'Backlog'],
      ['sprints', 'calendar-range', 'Sprints'],
    ]
      .map(([id, icon, text]) => navItem(id, icon, text, active).replace('class="nav-item', 'style="padding-left:30px" class="nav-item'))
      .join('');
    return head + sub;
  }).join('');

  nav.innerHTML = `
    <div class="row" style="gap:4px">
      <div class="org-switch" style="flex:1;margin:0"><span class="org-logo">L</span>Lumen Labs<i data-lucide="chevrons-up-down" style="width:14px;height:14px;color:var(--text-subtle)"></i></div>
      <button class="btn ghost sm icon" aria-label="Search"><i data-lucide="search"></i></button>
      <button class="btn secondary sm icon" aria-label="New issue"><i data-lucide="square-pen"></i></button>
    </div>
    <div style="height:8px"></div>
    ${navItem('inbox', 'inbox', 'Inbox', active, '<span class="count" style="color:var(--primary);font-weight:600">3</span>')}
    ${navItem('home', 'house', 'My work', active, '<span class="count">7</span>')}
    ${navItem('reports', 'chart-line', 'Reports', active)}
    <div class="nav-group">Projects</div>
    ${projects}
    <div class="nav-group">Team</div>
    ${navItem('members', 'users', 'Members', active)}
    <div class="spacer"></div>
    ${navItem('settings', 'settings', 'Settings', active)}
    <div class="row" style="padding:8px 8px 2px;gap:10px">
      <span class="avatar" data-name="Ada Lovelace"></span>
      <div style="line-height:1.2"><div style="font-weight:500;font-size:13px">Ada Lovelace</div><div style="font-size:12px;color:var(--text-muted)">Owner</div></div>
    </div>`;
});
