import { Injectable, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

const STORAGE_KEY = 'mydigitallibrary.lang';
const SUPPORTED_LANGS = ['bg', 'en'] as const;
export type SupportedLang = (typeof SUPPORTED_LANGS)[number];
const DEFAULT_LANG: SupportedLang = 'bg';

/** Plan section 9.2: default 'bg', chosen language persisted in localStorage. */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);

  init(): void {
    const stored = localStorage.getItem(STORAGE_KEY);
    const lang = (SUPPORTED_LANGS as readonly string[]).includes(stored ?? '') ? (stored as SupportedLang) : DEFAULT_LANG;
    this.transloco.setActiveLang(lang);
  }

  setLang(lang: SupportedLang): void {
    this.transloco.setActiveLang(lang);
    localStorage.setItem(STORAGE_KEY, lang);
  }

  get activeLang(): SupportedLang {
    return this.transloco.getActiveLang() as SupportedLang;
  }

  readonly supportedLangs = SUPPORTED_LANGS;
}
