import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap } from 'rxjs';
import { AntiforgeryService } from '../auth/antiforgery.service';

const MUTATING_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/** Attaches X-XSRF-TOKEN to every mutating request (plan section 8). */
export const antiforgeryInterceptor: HttpInterceptorFn = (req, next) => {
  if (!MUTATING_METHODS.has(req.method) || req.url.endsWith('/auth/antiforgery')) {
    return next(req);
  }

  const antiforgery = inject(AntiforgeryService);
  return antiforgery.getToken().pipe(
    switchMap((token) => next(req.clone({ setHeaders: { 'X-XSRF-TOKEN': token } }))),
  );
};
