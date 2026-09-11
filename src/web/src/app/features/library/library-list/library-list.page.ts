import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { LibraryItem, PagedResult } from '../../../core/api/models';

const EMPTY_PAGE: PagedResult<LibraryItem> = { items: [], page: 1, pageSize: 50, totalCount: 0 };

@Component({
  selector: 'app-library-list-page',
  imports: [RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library-list.page.html',
  styleUrl: './library-list.page.scss',
})
export class LibraryListPage {
  protected readonly searchQuery = signal('');

  // httpResource refetches automatically whenever searchQuery() changes —
  // no manual subscribe, no manual loading-state bookkeeping (plan section 9.1).
  protected readonly listResource = httpResource<PagedResult<LibraryItem>>(
    () => `/api/v1/library-items?q=${encodeURIComponent(this.searchQuery())}`,
    { defaultValue: EMPTY_PAGE },
  );

  protected onSearchInput(value: string): void {
    this.searchQuery.set(value);
  }
}
