import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { ImportLookupRequest, ImportLookupResult } from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class ImportApiService {
  private readonly http = inject(HttpClient);

  lookup(request: ImportLookupRequest): Observable<ImportLookupResult> {
    return this.http.post<ImportLookupResult>('/api/v1/import/lookup', request);
  }
}
