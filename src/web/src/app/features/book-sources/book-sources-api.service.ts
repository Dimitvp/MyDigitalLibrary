import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type {
  CreateFollowedBookSourceRequest,
  FollowedBookSource,
  UpdateFollowedBookSourceRequest,
} from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class BookSourcesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/book-sources';

  list(): Observable<FollowedBookSource[]> {
    return this.http.get<FollowedBookSource[]>(this.baseUrl);
  }

  create(request: CreateFollowedBookSourceRequest): Observable<FollowedBookSource> {
    return this.http.post<FollowedBookSource>(this.baseUrl, request);
  }

  update(id: string, request: UpdateFollowedBookSourceRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
