import type { Routes } from '@angular/router';
import AppShell from './app-shell';

/**
 * The screens inside the shell. The shell is this lazy route tree's own component, not a
 * `loadComponent` with children: that shape added 3.2 kB to the initial bundle (ADR 0006).
 */
export default [
  {
    path: '',
    component: AppShell,
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: 'Overview · Keryx',
        loadComponent: () => import('../pages/overview/overview'),
      },
      {
        path: 'articles',
        title: 'Articles · Keryx',
        loadComponent: () => import('../pages/articles/articles'),
      },
      {
        path: 'sources',
        title: 'Sources · Keryx',
        loadComponent: () => import('../pages/sources/sources'),
      },
      {
        path: 'api',
        title: 'API · Keryx',
        loadComponent: () => import('../pages/api/api'),
      },
      {
        path: 'settings',
        title: 'Settings · Keryx',
        loadComponent: () => import('../pages/settings/settings'),
      },
      {
        path: '**',
        title: 'Page not found · Keryx',
        loadComponent: () => import('../pages/not-found/not-found'),
      },
    ],
  },
] satisfies Routes;
