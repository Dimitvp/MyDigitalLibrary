import type { Genre } from '../core/api/models';
import type { SupportedLang } from '../core/i18n/language.service';

/** A genre's display label in the given UI language — falls back to the primary (Bulgarian) name when no English translation is set. */
export function genreDisplayName(genre: Genre, lang: SupportedLang): string {
  return lang === 'en' && genre.nameEn ? genre.nameEn : genre.name;
}

/**
 * Translates a bare genre name string (as carried on LibraryItem/WorkDetail's
 * `genreNames` — always the primary name, used as the resolve-or-create key)
 * to its display label in the given language, using the full genre list for
 * the name -> translation lookup.
 */
export function translateGenreName(name: string, genres: readonly Genre[], lang: SupportedLang): string {
  const genre = genres.find((g) => g.name === name);
  return genre ? genreDisplayName(genre, lang) : name;
}
