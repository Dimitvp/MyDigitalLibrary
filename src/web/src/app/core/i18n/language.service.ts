import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

const STORAGE_KEY = 'mydigitallibrary.lang';
const SUPPORTED_LANGS = ['bg', 'en'] as const;
export type SupportedLang = (typeof SUPPORTED_LANGS)[number];
const DEFAULT_LANG: SupportedLang = 'bg';

/** Plan section 9.2: default 'bg', chosen language persisted in localStorage. */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);

  // Mirrors Transloco's active language as a signal so components can react
  // to a language switch without touching the imperative getter below —
  // used for translating user-entered data (e.g. genre names) that Transloco
  // itself has no key for.
  private readonly _activeLangSignal = signal<SupportedLang>(DEFAULT_LANG);
  readonly activeLangSignal = this._activeLangSignal.asReadonly();

  init(): void {
    const stored = localStorage.getItem(STORAGE_KEY);
    const lang = (SUPPORTED_LANGS as readonly string[]).includes(stored ?? '') ? (stored as SupportedLang) : DEFAULT_LANG;
    this.transloco.setActiveLang(lang);
    this._activeLangSignal.set(lang);
  }

  setLang(lang: SupportedLang): void {
    this.transloco.setActiveLang(lang);
    this._activeLangSignal.set(lang);
    localStorage.setItem(STORAGE_KEY, lang);
  }

  get activeLang(): SupportedLang {
    return this.transloco.getActiveLang() as SupportedLang;
  }

  readonly supportedLangs = SUPPORTED_LANGS;
}
