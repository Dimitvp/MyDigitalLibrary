import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'library' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'library',
    canActivate: [authGuard],
    loadChildren: () => import('./features/library/library.routes').then((m) => m.libraryRoutes),
  },
  { path: '**', redirectTo: 'library' },
];
