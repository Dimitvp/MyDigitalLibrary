import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { BookFormat, Genre, LibraryItem, OwnershipStatus, PagedResult, StatisticsDto } from '../../../core/api/models';
import { LanguageService } from '../../../core/i18n/language.service';
import { genreDisplayName, translateGenreName } from '../../../shared/genre-display';
import { languageDisplayLabel } from '../../../shared/language-display';

const EMPTY_PAGE: PagedResult<LibraryItem> = { items: [], page: 1, pageSize: 100, totalCount: 0 };

const EMPTY_STATS: StatisticsDto = {
  year: new Date().getFullYear(),
  totalLibraryItems: 0,
  byFormat: {},
  byStatus: {},
  booksFinishedThisYear: 0,
  pagesReadThisYear: 0,
  currentlyReadingCount: 0,
  booksFinishedTotal: 0,
  averageRating: null,
  topAuthors: [],
};

const UNKNOWN_LANGUAGE = '￿'; // sorts after every real language code
const NO_GENRE = '￿'; // sorts after every real genre name
const SEARCH_DEBOUNCE_MS = 250;

// Sentinel for the "Без категория" filter option — not a real genre id, so it
// must never reach the API's genreId param (the backend has no notion of
// "genre is absent"); filtering for it happens client-side instead, same as
// the "Без категория" grouping bucket already does.
const NO_GENRE_FILTER = '__none__';

export type ReadingStatusFilter = '' | 'NotStarted' | 'Reading' | 'Finished' | 'Abandoned' | 'OnHold';
export type SortField = '' | 'title' | 'author' | 'acquiredOn';
export type SortDirection = 'asc' | 'desc';

interface GenreGroup {
  genreKey: string;
  genreLabel: string;
  items: LibraryItem[];
}

interface LanguageGroup {
  languageKey: string;
  languageLabel: string;
  genreGroups: GenreGroup[];
}

// Sorting stays a within-category ordering, never a reason to flatten the
// language/genre grouping — the user explicitly wants both at once.
function compareItems(a: LibraryItem, b: LibraryItem, sortBy: SortField, dir: SortDirection): number {
  const factor = dir === 'asc' ? 1 : -1;
  switch (sortBy) {
    case 'title':
      return factor * a.workTitle.localeCompare(b.workTitle);
    case 'author':
      return factor * (a.authorNames[0] ?? '').localeCompare(b.authorNames[0] ?? '');
    default:
      return factor * a.acquisition.acquiredOn.localeCompare(b.acquisition.acquiredOn);
  }
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
  protected readonly formatFilter = signal<BookFormat | ''>('');
  protected readonly readingStatusFilter = signal<ReadingStatusFilter>('');
  protected readonly ownershipStatusFilter = signal<OwnershipStatus | ''>('');
  protected readonly sortBy = signal<SortField>('');
  protected readonly sortDir = signal<SortDirection>('desc');

  // Keyed by languageKey (language groups) and `${languageKey}::${genreKey}`
  // (genre groups) rather than by display label, so collapsed state survives
  // a UI-language switch that changes the labels themselves.
  protected readonly collapsedLanguages = signal<ReadonlySet<string>>(new Set());
  protected readonly collapsedGenres = signal<ReadonlySet<string>>(new Set());

  private searchDebounceHandle: ReturnType<typeof setTimeout> | undefined;

  protected readonly genresResource = httpResource<Genre[]>(() => '/api/v1/genres', { defaultValue: [] });

  // httpResource refetches automatically whenever a signal read inside this
  // function changes — no manual subscribe, no manual loading-state
  // bookkeeping (plan section 9.1). pageSize=1000 covers the current
  // collection without full pagination UI yet (backend caps at
  // PageRequest.MaxPageSize regardless of what's requested here). Sorting is
  // applied client-side in `groups` below (author order isn't something the
  // API knows how to sort by), so no sortBy/sortDir params are sent here.
  protected readonly listResource = httpResource<PagedResult<LibraryItem>>(
    () => {
      const params = new URLSearchParams();
      params.set('q', this.searchQuery());
      params.set('pageSize', '1000');
      if (this.genreFilter() && this.genreFilter() !== NO_GENRE_FILTER) params.set('genreId', this.genreFilter());
      if (this.formatFilter()) params.set('format', this.formatFilter());
      if (this.readingStatusFilter()) params.set('readingStatus', this.readingStatusFilter());
      if (this.ownershipStatusFilter()) params.set('status', this.ownershipStatusFilter());
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
  protected readonly readCount = computed(() => this.statsResource.value().booksFinishedTotal);

  // Groups by language first (bg, en, then anything else, unset last), then by
  // genre within each language (unset genre last) — the two-level split the
  // user asked for, computed client-side since the whole collection is
  // already fetched in one page. The chosen sort field/direction orders the
  // items inside each genre bucket; it never flattens the grouping itself.
  protected readonly groups = computed<LanguageGroup[]>(() => {
    const allItems = this.listResource.value().items;
    const items = this.genreFilter() === NO_GENRE_FILTER ? allItems.filter((item) => item.genreNames.length === 0) : allItems;
    const genres = this.genresResource.value();
    const lang = this.language.activeLangSignal();
    const sortBy = this.sortBy();
    const sortDir = this.sortDir();

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
        languageKey: langKey,
        languageLabel: langKey === UNKNOWN_LANGUAGE ? 'Неозначен език' : languageDisplayLabel(langKey),
        genreGroups: genreKeys.map((genreKey) => ({
          genreKey,
          genreLabel: genreKey === NO_GENRE ? 'Без категория' : translateGenreName(genreKey, genres, lang),
          items: [...byGenre.get(genreKey)!].sort((a, b) => compareItems(a, b, sortBy, sortDir)),
        })),
      };
    });
  });

  protected genreOptionLabel(genre: Genre): string {
    return genreDisplayName(genre, this.language.activeLangSignal());
  }

  protected itemLanguageLabel(code: string | null): string | null {
    return code ? languageDisplayLabel(code) : null;
  }

  protected onSearchInput(value: string): void {
    clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => this.searchQuery.set(value), SEARCH_DEBOUNCE_MS);
  }

  protected onGenreFilterChange(value: string): void {
    this.genreFilter.set(value);
  }

  protected onFormatFilterChange(value: string): void {
    this.formatFilter.set(value as BookFormat | '');
  }

  // The stat cards double as quick filters — clicking one narrows the list to
  // just those books; clicking the active one again clears back to "all".
  protected isStatActive(kind: 'owned' | 'borrowed' | 'lentout' | 'reading' | 'read'): boolean {
    if (kind === 'reading') return this.readingStatusFilter() === 'Reading';
    if (kind === 'read') return this.readingStatusFilter() === 'Finished';
    const statusByKind: Record<'owned' | 'borrowed' | 'lentout', OwnershipStatus> = {
      owned: 'Owned',
      borrowed: 'Borrowed',
      lentout: 'LentOut',
    };
    return this.ownershipStatusFilter() === statusByKind[kind];
  }

  protected onStatClick(kind: 'all' | 'owned' | 'borrowed' | 'lentout' | 'reading' | 'read'): void {
    const isReadingStatusKind = kind === 'reading' || kind === 'read';

    if (!isReadingStatusKind && kind !== 'all' && this.isStatActive(kind)) {
      this.ownershipStatusFilter.set('');
      return;
    }
    if (isReadingStatusKind && this.isStatActive(kind)) {
      this.readingStatusFilter.set('');
      return;
    }

    this.ownershipStatusFilter.set('');
    this.readingStatusFilter.set('');

    switch (kind) {
      case 'owned':
        this.ownershipStatusFilter.set('Owned');
        break;
      case 'borrowed':
        this.ownershipStatusFilter.set('Borrowed');
        break;
      case 'lentout':
        this.ownershipStatusFilter.set('LentOut');
        break;
      case 'reading':
        this.readingStatusFilter.set('Reading');
        break;
      case 'read':
        this.readingStatusFilter.set('Finished');
        break;
    }
  }

  protected onReadingStatusFilterChange(value: string): void {
    this.readingStatusFilter.set(value as ReadingStatusFilter);
  }

  protected onSortByChange(value: string): void {
    const field = value as SortField;
    this.sortBy.set(field);
    this.sortDir.set(field === 'title' || field === 'author' ? 'asc' : 'desc');
  }

  protected toggleSortDir(): void {
    this.sortDir.update((dir) => (dir === 'asc' ? 'desc' : 'asc'));
  }

  protected isLanguageCollapsed(languageKey: string): boolean {
    return this.collapsedLanguages().has(languageKey);
  }

  protected toggleLanguageCollapsed(languageKey: string): void {
    this.collapsedLanguages.update((set) => {
      const next = new Set(set);
      next.has(languageKey) ? next.delete(languageKey) : next.add(languageKey);
      return next;
    });
  }

  protected isGenreCollapsed(languageKey: string, genreKey: string): boolean {
    return this.collapsedGenres().has(`${languageKey}::${genreKey}`);
  }

  protected toggleGenreCollapsed(languageKey: string, genreKey: string): void {
    const key = `${languageKey}::${genreKey}`;
    this.collapsedGenres.update((set) => {
      const next = new Set(set);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });
  }

  protected scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected scrollToBottom(): void {
    window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' });
  }
}
