import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/app';
import './index.css';
// Keeps the theme in sync with the OS and other tabs from the first render.
import '@/shared/theme';

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root element #root not found.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
