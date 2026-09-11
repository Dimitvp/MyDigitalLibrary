import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { CreateLibraryItemRequest, LibraryItem, PagedResult } from '../../core/api/models';

export interface ListLibraryItemsParams {
  q?: string;
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

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
