import { Routes } from '@angular/router';

export const wishlistRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./wishlist-list/wishlist-list.page').then((m) => m.WishlistListPage),
  },
  {
    path: 'add',
    loadComponent: () => import('./wishlist-form/wishlist-form.page').then((m) => m.WishlistFormPage),
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./wishlist-edit/wishlist-edit.page').then((m) => m.WishlistEditPage),
  },
  {
    path: ':id/fulfill',
    loadComponent: () => import('./wishlist-fulfill/wishlist-fulfill.page').then((m) => m.WishlistFulfillPage),
  },
];
