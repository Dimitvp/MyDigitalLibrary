import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import type { ProblemDetails } from '../api/models';
import { NotificationService } from '../../shared/ui/notification/notification.service';

/**
 * Plan section 4/9.2: the API returns a stable errorCode, never translated
 * text — this maps it to a translation key (errors.<errorCode>, falling back
 * to errors.generic) rather than showing the raw ProblemDetails.detail.
 * 401s are excluded: the auth guard/redirect already handles that case.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status !== 401) {
        const problem = error.error as ProblemDetails | null;
        notifications.show(problem?.errorCode ? `errors.${problem.errorCode}` : 'errors.generic');
      }
      return throwError(() => error);
    }),
  );
};
