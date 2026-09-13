import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type {
  CreateWishlistEntryRequest,
  FindCoverResult,
  FulfillWishlistEntryRequest,
  LibraryItem,
  UpdateWishlistEntryRequest,
  WishlistEntry,
} from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class WishlistApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/wishlist';

  list(): Observable<WishlistEntry[]> {
    return this.http.get<WishlistEntry[]>(this.baseUrl);
  }

  create(request: CreateWishlistEntryRequest): Observable<WishlistEntry> {
    return this.http.post<WishlistEntry>(this.baseUrl, request);
  }

  update(id: string, request: UpdateWishlistEntryRequest): Observable<WishlistEntry> {
    return this.http.put<WishlistEntry>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  fulfill(id: string, request: FulfillWishlistEntryRequest): Observable<LibraryItem> {
    return this.http.post<LibraryItem>(`${this.baseUrl}/${id}/fulfill`, request);
  }

  findCover(id: string): Observable<FindCoverResult> {
    return this.http.post<FindCoverResult>(`${this.baseUrl}/${id}/find-cover`, {});
  }
}
