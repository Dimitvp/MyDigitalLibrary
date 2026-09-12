import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { LibraryItem, PagedResult, StatisticsDto } from '../../../core/api/models';

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
  protected readonly searchQuery = signal('');

  // httpResource refetches automatically whenever searchQuery() changes —
  // no manual subscribe, no manual loading-state bookkeeping (plan section 9.1).
  // pageSize=100 covers the current collection without full pagination UI yet.
  protected readonly listResource = httpResource<PagedResult<LibraryItem>>(
    () => `/api/v1/library-items?q=${encodeURIComponent(this.searchQuery())}&pageSize=100`,
    { defaultValue: EMPTY_PAGE },
  );

  protected readonly statsResource = httpResource<StatisticsDto>(() => '/api/v1/statistics', {
    defaultValue: EMPTY_STATS,
  });

  protected readonly ownedCount = computed(() => this.statsResource.value().byStatus['Owned'] ?? 0);
  protected readonly borrowedCount = computed(() => this.statsResource.value().byStatus['Borrowed'] ?? 0);
  protected readonly lentOutCount = computed(() => this.statsResource.value().byStatus['LentOut'] ?? 0);

  // Groups by language first (bg, en, then anything else, unset last), then by
  // genre within each language (unset genre last) — the two-level split the
  // user asked for, computed client-side since the whole collection is
  // already fetched in one page.
  protected readonly groups = computed<LanguageGroup[]>(() => {
    const items = this.listResource.value().items;

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
          genreLabel: genreKey === NO_GENRE ? 'Без категория' : genreKey,
          items: byGenre.get(genreKey)!,
        })),
      };
    });
  });

  protected onSearchInput(value: string): void {
    this.searchQuery.set(value);
  }
}
