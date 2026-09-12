import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { Genre, UpdateWorkRequest } from './models';

@Injectable({ providedIn: 'root' })
export class CatalogApiService {
  private readonly http = inject(HttpClient);

  genres(): Observable<Genre[]> {
    return this.http.get<Genre[]>('/api/v1/genres');
  }

  updateWork(workId: string, request: UpdateWorkRequest): Observable<void> {
    return this.http.put<void>(`/api/v1/works/${workId}`, request);
  }

  uploadCover(editionId: string, file: File): Observable<{ coverImageUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ coverImageUrl: string }>(`/api/v1/editions/${editionId}/cover`, formData);
  }
}
