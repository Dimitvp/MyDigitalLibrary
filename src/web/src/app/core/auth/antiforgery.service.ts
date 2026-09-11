import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';

interface AntiforgeryResponse {
  token: string;
}

/**
 * Caches the antiforgery token for the session (plan section 8: every
 * state-changing request needs it). Call invalidate() to force a refetch —
 * e.g. after the server rejects a token as stale.
 */
@Injectable({ providedIn: 'root' })
export class AntiforgeryService {
  private readonly http = inject(HttpClient);
  private cached$: Observable<string> | null = null;

  getToken(): Observable<string> {
    this.cached$ ??= this.http.get<AntiforgeryResponse>('/api/v1/auth/antiforgery').pipe(
      map((response) => response.token),
      shareReplay(1),
    );
    return this.cached$;
  }

  invalidate(): void {
    this.cached$ = null;
  }
}
