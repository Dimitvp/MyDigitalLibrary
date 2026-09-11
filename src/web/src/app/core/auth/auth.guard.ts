import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const redirectToLogin = () => router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

  const current = auth.currentUser();
  if (current !== undefined) {
    return current !== null || redirectToLogin();
  }

  return auth.fetchCurrentUser().pipe(
    map(() => true),
    catchError(() => of(redirectToLogin())),
  );
};
