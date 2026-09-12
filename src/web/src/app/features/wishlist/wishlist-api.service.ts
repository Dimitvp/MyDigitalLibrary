import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { CreateWishlistEntryRequest, WishlistEntry } from '../../core/api/models';

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
}
