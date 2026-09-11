import { Routes } from '@angular/router';

export const libraryRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./library-list/library-list.page').then((m) => m.LibraryListPage),
  },
  {
    path: 'add',
    loadComponent: () => import('./library-form/library-form.page').then((m) => m.LibraryFormPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./library-detail/library-detail.page').then((m) => m.LibraryDetailPage),
  },
];
