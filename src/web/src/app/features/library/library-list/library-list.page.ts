import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { Genre, LibraryItem, PagedResult, StatisticsDto } from '../../../core/api/models';
import { LanguageService } from '../../../core/i18n/language.service';
import { genreDisplayName, translateGenreName } from '../../../shared/genre-display';

const EMPTY_PAGE: PagedResult<LibraryItem> = { items: [], page: 1, pageSize: 100, totalCount: 0 };

const EMPTY_STATS: StatisticsDto = {
  year: new Date().getFullYear(),
  totalLibraryItems: 0,
  byFormat: {},
  byStatus: {},
  booksFinishedThisYear: 0,
  pagesReadThisYear: 0,
  currentlyReadingCount: 0,
  averageRating: null,
  topAuthors: [],
};

const LANGUAGE_LABELS: Record<string, string> = { bg: 'Български', en: 'English' };
const UNKNOWN_LANGUAGE = '￿'; // sorts after every real language code
const NO_GENRE = '￿'; // sorts after every real genre name
const SEARCH_DEBOUNCE_MS = 250;

export type ReadingStatusFilter = '' | 'NotStarted' | 'Reading' | 'Finished' | 'Abandoned' | 'OnHold';
export type SortField = '' | 'title' | 'acquiredOn';
export type SortDirection = 'asc' | 'desc';

interface GenreGroup {
  genreLabel: string;
  items: LibraryItem[];
}

interface LanguageGroup {
  languageLabel: string;
  genreGroups: GenreGroup[];
}

@Component({
  selector: 'app-library-list-page',
  imports: [RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library-list.page.html',
  styleUrl: './library-list.page.scss',
})
export class LibraryListPage {
  protected readonly language = inject(LanguageService);

  protected readonly searchQuery = signal('');
  protected readonly genreFilter = signal('');
  protected readonly readingStatusFilter = signal<ReadingStatusFilter>('');
  protected readonly sortBy = signal<SortField>('');
  protected readonly sortDir = signal<SortDirection>('desc');

  private searchDebounceHandle: ReturnType<typeof setTimeout> | undefined;

  protected readonly genresResource = httpResource<Genre[]>(() => '/api/v1/genres', { defaultValue: [] });

  // httpResource refetches automatically whenever a signal read inside this
  // function changes — no manual subscribe, no manual loading-state
  // bookkeeping (plan section 9.1). pageSize=100 covers the current
  // collection without full pagination UI yet.
  protected readonly listResource = httpResource<PagedResult<LibraryItem>>(
    () => {
      const params = new URLSearchParams();
      params.set('q', this.searchQuery());
      params.set('pageSize', '100');
      if (this.genreFilter()) params.set('genreId', this.genreFilter());
      if (this.readingStatusFilter()) params.set('readingStatus', this.readingStatusFilter());
      if (this.sortBy()) {
        params.set('sortBy', this.sortBy());
        params.set('sortDir', this.sortDir());
      }
      return `/api/v1/library-items?${params.toString()}`;
    },
    { defaultValue: EMPTY_PAGE },
  );

  protected readonly statsResource = httpResource<StatisticsDto>(() => '/api/v1/statistics', {
    defaultValue: EMPTY_STATS,
  });

  protected readonly ownedCount = computed(() => this.statsResource.value().byStatus['Owned'] ?? 0);
  protected readonly borrowedCount = computed(() => this.statsResource.value().byStatus['Borrowed'] ?? 0);
  protected readonly lentOutCount = computed(() => this.statsResource.value().byStatus['LentOut'] ?? 0);

  // Sorting is a flat, explicit view the user opted into; the default
  // language/genre grouping stays the browsing view when no sort is picked.
  protected readonly isSorted = computed(() => this.sortBy() !== '');
  protected readonly sortedItems = computed(() => this.listResource.value().items);

  // Groups by language first (bg, en, then anything else, unset last), then by
  // genre within each language (unset genre last) — the two-level split the
  // user asked for, computed client-side since the whole collection is
  // already fetched in one page.
  protected readonly groups = computed<LanguageGroup[]>(() => {
    const items = this.listResource.value().items;
    const genres = this.genresResource.value();
    const lang = this.language.activeLangSignal();

    const byLanguage = new Map<string, LibraryItem[]>();
    for (const item of items) {
      const key = item.language ?? UNKNOWN_LANGUAGE;
      const bucket = byLanguage.get(key);
      if (bucket) bucket.push(item);
      else byLanguage.set(key, [item]);
    }

    const languageKeys = [...byLanguage.keys()].sort((a, b) => a.localeCompare(b));

    return languageKeys.map((langKey) => {
      const languageItems = byLanguage.get(langKey)!;
      const byGenre = new Map<string, LibraryItem[]>();

      for (const item of languageItems) {
        const key = item.genreNames[0] ?? NO_GENRE;
        const bucket = byGenre.get(key);
        if (bucket) bucket.push(item);
        else byGenre.set(key, [item]);
      }

      const genreKeys = [...byGenre.keys()].sort((a, b) => a.localeCompare(b));

      return {
        languageLabel:
          langKey === UNKNOWN_LANGUAGE ? 'Неозначен език' : (LANGUAGE_LABELS[langKey] ?? langKey.toUpperCase()),
        genreGroups: genreKeys.map((genreKey) => ({
          genreLabel: genreKey === NO_GENRE ? 'Без категория' : translateGenreName(genreKey, genres, lang),
          items: byGenre.get(genreKey)!,
        })),
      };
    });
  });

  protected genreOptionLabel(genre: Genre): string {
    return genreDisplayName(genre, this.language.activeLangSignal());
  }

  protected onSearchInput(value: string): void {
    clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => this.searchQuery.set(value), SEARCH_DEBOUNCE_MS);
  }

  protected onGenreFilterChange(value: string): void {
    this.genreFilter.set(value);
  }

  protected onReadingStatusFilterChange(value: string): void {
    this.readingStatusFilter.set(value as ReadingStatusFilter);
  }

  protected onSortByChange(value: string): void {
    this.sortBy.set(value as SortField);
  }

  protected toggleSortDir(): void {
    this.sortDir.update((dir) => (dir === 'asc' ? 'desc' : 'asc'));
  }
}
