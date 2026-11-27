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
        title: 'Admin · Keryx',
        loadComponent: () => import('../pages/home-placeholder/home-placeholder'),
      },
      {
        path: 'sources',
        title: 'Sources · Keryx',
        loadComponent: () => import('../pages/sources/sources'),
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
