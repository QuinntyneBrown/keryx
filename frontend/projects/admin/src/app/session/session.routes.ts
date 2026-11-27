import type { Routes } from '@angular/router';
import { SESSION_API } from '@kx/api';
import { HttpSessionApi } from './http-session-api';
import { staffGuard } from './staff-guard';

export default [
  {
    path: '',
    providers: [{ provide: SESSION_API, useClass: HttpSessionApi }],
    children: [
      {
        path: 'forbidden',
        title: 'Access denied · Keryx',
        loadComponent: () => import('../pages/forbidden/forbidden'),
      },
      {
        path: 'sign-in',
        title: 'Sign in · Keryx',
        loadComponent: () => import('../pages/sign-in/sign-in'),
      },
      {
        path: '',
        canMatch: [staffGuard],
        children: [
          {
            path: '',
            pathMatch: 'full',
            title: 'Admin · Keryx',
            loadComponent: () => import('../shell/app-shell'),
          },
          { path: '**', redirectTo: '' },
        ],
      },
    ],
  },
] satisfies Routes;
