import { Routes } from '@angular/router';

export const bookSourcesRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./book-sources-list/book-sources-list.page').then((m) => m.BookSourcesListPage),
  },
];
