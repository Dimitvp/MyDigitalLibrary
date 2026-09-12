import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { Acquisition, CreateLibraryItemRequest, LibraryItem, OwnershipStatus, PagedResult } from '../../core/api/models';

export interface ListLibraryItemsParams {
  q?: string;
  genreId?: string;
  readingStatus?: string;
  sortBy?: 'title' | 'acquiredOn';
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class LibraryApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/library-items';

  list(params: ListLibraryItemsParams = {}): Observable<PagedResult<LibraryItem>> {
    let httpParams = new HttpParams();
    if (params.q) httpParams = httpParams.set('q', params.q);
    if (params.genreId) httpParams = httpParams.set('genreId', params.genreId);
    if (params.readingStatus) httpParams = httpParams.set('readingStatus', params.readingStatus);
    if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDir) httpParams = httpParams.set('sortDir', params.sortDir);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PagedResult<LibraryItem>>(this.baseUrl, { params: httpParams });
  }

  get(id: string): Observable<LibraryItem> {
    return this.http.get<LibraryItem>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateLibraryItemRequest): Observable<LibraryItem> {
    return this.http.post<LibraryItem>(this.baseUrl, request);
  }

  updateNote(id: string, acquisition: Acquisition, personalNote: string | null): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, { acquisition, personalNote });
  }

  updateStatus(id: string, status: OwnershipStatus): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/status`, { status });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
