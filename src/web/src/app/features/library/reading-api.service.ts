import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { ReadingSessionInfo } from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class ReadingApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/reading-sessions';

  list(libraryItemId: string): Observable<ReadingSessionInfo[]> {
    return this.http.get<ReadingSessionInfo[]>(this.baseUrl, {
      params: new HttpParams().set('libraryItemId', libraryItemId),
    });
  }

  start(libraryItemId: string, startedOn: string): Observable<ReadingSessionInfo> {
    return this.http.post<ReadingSessionInfo>(this.baseUrl, { libraryItemId, startedOn });
  }

  finish(sessionId: string, endedOn: string): Observable<ReadingSessionInfo> {
    return this.http.post<ReadingSessionInfo>(`${this.baseUrl}/${sessionId}/finish`, { endedOn });
  }
}
