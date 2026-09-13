import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { BookFormat, Genre, WishlistEntry } from '../../../core/api/models';
import { LanguageService } from '../../../core/i18n/language.service';
import { genreDisplayName, translateGenreName } from '../../../shared/genre-display';
import { languageDisplayLabel } from '../../../shared/language-display';
import { WishlistApiService } from '../wishlist-api.service';

const UNKNOWN_LANGUAGE = '￿'; // sorts after every real language code
const NO_GENRE = '￿'; // sorts after every real genre name
const SEARCH_DEBOUNCE_MS = 250;

// Deliberate pause between find-cover calls — each one may hit Google Books'
// shared anonymous quota (see GoogleBooksProvider), which is easy to exhaust
// with a tight loop over ~100+ entries. A gap trades a slower bulk run for
// not burning through it in a few seconds.
const FIND_COVER_DELAY_MS = 600;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type SortField = 'addedOn' | 'title' | 'author' | 'priority';
export type SortDirection = 'asc' | 'desc';

interface GenreGroup {
  genreKey: string;
  genreLabel: string;
  entries: WishlistEntry[];
}

interface LanguageGroup {
  languageKey: string;
  languageLabel: string;
  genreGroups: GenreGroup[];
}

function matchesSearch(entry: WishlistEntry, query: string): boolean {
  if (!query) return true;
  const haystack = `${entry.workTitle} ${entry.authorNames.join(' ')}`.toLowerCase();
  return haystack.includes(query);
}

function compareEntries(a: WishlistEntry, b: WishlistEntry, sortBy: SortField, dir: SortDirection): number {
  const factor = dir === 'asc' ? 1 : -1;
  switch (sortBy) {
    case 'title':
      return factor * a.workTitle.localeCompare(b.workTitle);
    case 'author':
      return factor * (a.authorNames[0] ?? '').localeCompare(b.authorNames[0] ?? '');
    case 'priority':
      return factor * (a.priority - b.priority);
    default:
      return factor * a.addedOn.localeCompare(b.addedOn);
  }
}

@Component({
  selector: 'app-wishlist-list-page',
  imports: [RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-list.page.html',
  styleUrl: './wishlist-list.page.scss',
})
export class WishlistListPage {
  private readonly language = inject(LanguageService);
  private readonly api = inject(WishlistApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly searchQuery = signal('');
  protected readonly genreFilter = signal('');
  protected readonly formatFilter = signal<BookFormat | ''>('');
  protected readonly priorityFilter = signal<'' | number>('');
  protected readonly outOfStockOnly = signal(false);
  protected readonly sortBy = signal<SortField>('addedOn');
  protected readonly sortDir = signal<SortDirection>('desc');

  protected readonly collapsedLanguages = signal<ReadonlySet<string>>(new Set());
  protected readonly collapsedGenres = signal<ReadonlySet<string>>(new Set());

  protected readonly deletingId = signal<string | null>(null);

  protected readonly findingCovers = signal(false);
  protected readonly findCoverProgress = signal<{ checked: number; total: number; found: number } | null>(null);
  private stopFindingCovers = false;

  private searchDebounceHandle: ReturnType<typeof setTimeout> | undefined;

  protected readonly listResource = httpResource<WishlistEntry[]>(() => '/api/v1/wishlist', {
    defaultValue: [],
  });

  protected readonly genresResource = httpResource<Genre[]>(() => '/api/v1/genres', { defaultValue: [] });

  protected readonly totalCount = computed(() => this.listResource.value().length);
  protected readonly outOfStockCount = computed(() => this.listResource.value().filter((e) => e.isOutOfStock).length);
  protected readonly missingCoverCount = computed(
    () => this.listResource.value().filter((e) => !e.coverImageUrl && !e.preferredEditionId).length,
  );

  protected readonly filteredEntries = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const genreId = this.genreFilter();
    const format = this.formatFilter();
    const priority = this.priorityFilter();
    const outOfStockOnly = this.outOfStockOnly();
    const genres = this.genresResource.value();

    return this.listResource.value().filter((entry) => {
      if (!matchesSearch(entry, query)) return false;
      if (format && entry.desiredFormat !== format) return false;
      if (priority !== '' && entry.priority !== priority) return false;
      if (outOfStockOnly && !entry.isOutOfStock) return false;
      if (genreId) {
        const genre = genres.find((g) => g.id === genreId);
        if (!genre || !entry.genreNames.includes(genre.name)) return false;
      }
      return true;
    });
  });

  protected readonly groups = computed<LanguageGroup[]>(() => {
    const entries = this.filteredEntries();
    const genres = this.genresResource.value();
    const lang = this.language.activeLangSignal();
    const sortBy = this.sortBy();
    const sortDir = this.sortDir();

    const byLanguage = new Map<string, WishlistEntry[]>();
    for (const entry of entries) {
      const key = entry.language ?? UNKNOWN_LANGUAGE;
      const bucket = byLanguage.get(key);
      if (bucket) bucket.push(entry);
      else byLanguage.set(key, [entry]);
    }

    const languageKeys = [...byLanguage.keys()].sort((a, b) => a.localeCompare(b));

    return languageKeys.map((langKey) => {
      const languageEntries = byLanguage.get(langKey)!;
      const byGenre = new Map<string, WishlistEntry[]>();

      for (const entry of languageEntries) {
        const key = entry.genreNames[0] ?? NO_GENRE;
        const bucket = byGenre.get(key);
        if (bucket) bucket.push(entry);
        else byGenre.set(key, [entry]);
      }

      const genreKeys = [...byGenre.keys()].sort((a, b) => a.localeCompare(b));

      return {
        languageKey: langKey,
        languageLabel: langKey === UNKNOWN_LANGUAGE ? 'Неозначен език' : languageDisplayLabel(langKey),
        genreGroups: genreKeys.map((genreKey) => ({
          genreKey,
          genreLabel: genreKey === NO_GENRE ? 'Без категория' : translateGenreName(genreKey, genres, lang),
          entries: [...byGenre.get(genreKey)!].sort((a, b) => compareEntries(a, b, sortBy, sortDir)),
        })),
      };
    });
  });

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.stopFindingCovers = true;
    });
  }

  // Sequential, not parallel: each call may hit Google Books' shared quota
  // (see FIND_COVER_DELAY_MS) and a title-based match is uncertain enough
  // that a runaway Promise.all wouldn't be safe to just fire and forget.
  protected async findMissingCovers(): Promise<void> {
    if (this.findingCovers()) return;

    const candidates = this.listResource.value().filter((e) => !e.coverImageUrl && !e.preferredEditionId);
    if (candidates.length === 0) return;

    this.stopFindingCovers = false;
    this.findingCovers.set(true);
    this.findCoverProgress.set({ checked: 0, total: candidates.length, found: 0 });

    for (const entry of candidates) {
      if (this.stopFindingCovers) break;

      try {
        const result = await firstValueFrom(this.api.findCover(entry.id));
        if (result.found) {
          this.listResource.value.update((entries) =>
            entries.map((e) =>
              e.id === entry.id
                ? { ...e, coverImageUrl: result.coverUrl, preferredEditionId: result.entry.preferredEditionId }
                : e,
            ),
          );
        }
        this.findCoverProgress.update((p) => (p ? { ...p, checked: p.checked + 1, found: p.found + (result.found ? 1 : 0) } : p));
      } catch {
        this.findCoverProgress.update((p) => (p ? { ...p, checked: p.checked + 1 } : p));
      }

      if (this.stopFindingCovers) break;
      await sleep(FIND_COVER_DELAY_MS);
    }

    this.findingCovers.set(false);
  }

  protected stopFindMissingCovers(): void {
    this.stopFindingCovers = true;
  }

  protected entryLanguageLabel(code: string | null): string | null {
    return code ? languageDisplayLabel(code) : null;
  }

  // Priority is 1 (highest) to 5 (lowest) — see wishlist.form.priority. Shown
  // as a word next to the number since a bare "3" reads as meaningless.
  protected priorityLabel(priority: number): string {
    return this.transloco.translate(`wishlist.priorityLevel.${priority}`);
  }

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

  protected onFormatFilterChange(value: string): void {
    this.formatFilter.set(value as BookFormat | '');
  }

  protected onPriorityFilterChange(value: string): void {
    this.priorityFilter.set(value === '' ? '' : Number(value));
  }

  protected toggleOutOfStockOnly(): void {
    this.outOfStockOnly.update((v) => !v);
  }

  protected onSortByChange(value: string): void {
    const field = value as SortField;
    this.sortBy.set(field);
    this.sortDir.set(field === 'title' || field === 'author' || field === 'priority' ? 'asc' : 'desc');
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

  protected deleteEntry(entry: WishlistEntry): void {
    if (this.deletingId()) return;
    if (!window.confirm(this.transloco.translate('wishlist.list.deleteConfirm'))) return;

    this.deletingId.set(entry.id);
    this.api
      .delete(entry.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deletingId.set(null);
          this.listResource.reload();
        },
        error: () => this.deletingId.set(null),
      });
  }

  protected scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected scrollToBottom(): void {
    window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' });
  }
}
