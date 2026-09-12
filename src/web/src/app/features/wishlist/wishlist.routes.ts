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
];
