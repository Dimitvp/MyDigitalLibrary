import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, switchMap, tap } from 'rxjs';

export interface CurrentUser {
  id: string;
  email: string;
}

/**
 * currentUser is `undefined` until the first /auth/me check completes (see
 * authGuard) — that's distinct from `null` (checked, not authenticated), so
 * the app never briefly renders as "logged out" before it actually knows.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _currentUser = signal<CurrentUser | null | undefined>(undefined);
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() != null);

  login(email: string, password: string): Observable<CurrentUser> {
    return this.http
      .post<void>('/api/v1/auth/login', { email, password })
      .pipe(switchMap(() => this.fetchCurrentUser()));
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/v1/auth/logout', {}).pipe(tap(() => this._currentUser.set(null)));
  }

  fetchCurrentUser(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>('/api/v1/auth/me').pipe(tap((user) => this._currentUser.set(user)));
  }
}
