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
  {
    path: 'wishlist',
    canActivate: [authGuard],
    loadChildren: () => import('./features/wishlist/wishlist.routes').then((m) => m.wishlistRoutes),
  },
  {
    path: 'book-sources',
    canActivate: [authGuard],
    loadChildren: () => import('./features/book-sources/book-sources.routes').then((m) => m.bookSourcesRoutes),
  },
  {
    path: 'import',
    canActivate: [authGuard],
    loadComponent: () => import('./features/import/import.page').then((m) => m.ImportPage),
  },
  { path: '**', redirectTo: 'library' },
];
