import type { Routes } from '@angular/router';

/**
 * Every route loads its component lazily (AGENTS.md, "Lazy loading and memory"): nothing a
 * screen draws is in the initial bundle.
 *
 * Until the screens exist the only route is the shell placeholder of ADR 0004, and any other
 * address goes to it. Journey 2 gives the shell its child routes and the Not found screen.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Admin · Keryx',
    loadComponent: () => import('./shell/app-shell'),
  },
  { path: '**', redirectTo: '' },
];
