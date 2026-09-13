import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import type { ProblemDetails } from '../api/models';
import { NotificationService } from '../../shared/ui/notification/notification.service';
import { AntiforgeryService } from '../auth/antiforgery.service';

/**
 * Plan section 4/9.2: the API returns a stable errorCode, never translated
 * text — this maps it to a translation key (errors.<errorCode>, falling back
 * to errors.generic) rather than showing the raw ProblemDetails.detail.
 * 401s are excluded: the auth guard/redirect already handles that case.
 *
 * A stale anti-forgery token (cached for the whole SPA session — see
 * AntiforgeryService) is recoverable without bothering the user: drop the
 * cached token, fetch a fresh one, and retry the request exactly once
 * before falling back to the generic error toast.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);
  const antiforgery = inject(AntiforgeryService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && (error.error as ProblemDetails | null)?.errorCode === 'antiforgery.invalid_token') {
        antiforgery.invalidate();
        return antiforgery.getToken().pipe(
          switchMap((token) => next(req.clone({ setHeaders: { 'X-XSRF-TOKEN': token } }))),
          catchError((retryError: unknown) => {
            notifyGenericFailure(retryError);
            return throwError(() => retryError);
          }),
        );
      }

      notifyGenericFailure(error);
      return throwError(() => error);
    }),
  );

  // Static asset requests (e.g. i18n files) aren't API calls and never carry a
  // ProblemDetails body. Notifying about them here would also depend on the
  // very translations that just failed to load, risking a load/notify feedback loop.
  function notifyGenericFailure(error: unknown): void {
    const isAssetRequest = req.url.startsWith('/assets/');
    if (!isAssetRequest && error instanceof HttpErrorResponse && error.status !== 401) {
      const problem = error.error as ProblemDetails | null;
      const params = typeof problem?.['existingWorkTitle'] === 'string' ? { title: problem['existingWorkTitle'] } : undefined;
      notifications.show(problem?.errorCode ? `errors.${problem.errorCode}` : 'errors.generic', params);
    }
  }
};
