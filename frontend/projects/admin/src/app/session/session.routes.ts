import type { Routes } from '@angular/router';
import { CURRENT_STAFF, SESSION_API } from '@kx/api';
import { CurrentStaffStore } from './current-staff-store';
import { HttpSessionApi } from './http-session-api';
import { staffGuard } from './staff-guard';

export default [
  {
    path: '',
    providers: [
      { provide: SESSION_API, useClass: HttpSessionApi },
      { provide: CURRENT_STAFF, useClass: CurrentStaffStore },
    ],
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
        loadChildren: () => import('../shell/shell.routes'),
      },
    ],
  },
] satisfies Routes;
