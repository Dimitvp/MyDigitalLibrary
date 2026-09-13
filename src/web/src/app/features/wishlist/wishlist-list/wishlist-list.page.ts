import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { Genre, WishlistEntry } from '../../../core/api/models';
import { LanguageService } from '../../../core/i18n/language.service';
import { translateGenreName } from '../../../shared/genre-display';
import { languageDisplayLabel } from '../../../shared/language-display';

const UNKNOWN_LANGUAGE = '￿'; // sorts after every real language code
const NO_GENRE = '￿'; // sorts after every real genre name

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

@Component({
  selector: 'app-wishlist-list-page',
  imports: [RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-list.page.html',
  styleUrl: './wishlist-list.page.scss',
})
export class WishlistListPage {
  private readonly language = inject(LanguageService);

  // A separate dataset from the library (/api/v1/wishlist, never merged with
  // /api/v1/library-items) — only the card/grouping visuals are shared with
  // the library list, so wishlist items never mix with owned ones.
  protected readonly listResource = httpResource<WishlistEntry[]>(() => '/api/v1/wishlist', {
    defaultValue: [],
  });

  protected readonly genresResource = httpResource<Genre[]>(() => '/api/v1/genres', { defaultValue: [] });

  // Same language-then-genre grouping as the library list.
  protected readonly groups = computed<LanguageGroup[]>(() => {
    const entries = this.listResource.value();
    const genres = this.genresResource.value();
    const lang = this.language.activeLangSignal();

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
          entries: byGenre.get(genreKey)!,
        })),
      };
    });
  });

  protected entryLanguageLabel(code: string | null): string | null {
    return code ? languageDisplayLabel(code) : null;
  }
}
