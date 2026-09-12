import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { Edition, Genre, UpdateEditionRequest, UpdateWorkRequest, WorkDetail } from './models';

@Injectable({ providedIn: 'root' })
export class CatalogApiService {
  private readonly http = inject(HttpClient);

  genres(): Observable<Genre[]> {
    return this.http.get<Genre[]>('/api/v1/genres');
  }

  renameGenre(genreId: string, name: string, nameEn: string | null): Observable<void> {
    return this.http.put<void>(`/api/v1/genres/${genreId}`, { name, nameEn });
  }

  deleteGenre(genreId: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/genres/${genreId}`);
  }

  getWork(workId: string): Observable<WorkDetail> {
    return this.http.get<WorkDetail>(`/api/v1/works/${workId}`);
  }

  updateWork(workId: string, request: UpdateWorkRequest): Observable<void> {
    return this.http.put<void>(`/api/v1/works/${workId}`, request);
  }

  getEdition(editionId: string): Observable<Edition> {
    return this.http.get<Edition>(`/api/v1/editions/${editionId}`);
  }

  updateEdition(editionId: string, request: UpdateEditionRequest): Observable<void> {
    return this.http.put<void>(`/api/v1/editions/${editionId}`, request);
  }

  uploadCover(editionId: string, file: File): Observable<{ coverImageUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ coverImageUrl: string }>(`/api/v1/editions/${editionId}/cover`, formData);
  }
}
