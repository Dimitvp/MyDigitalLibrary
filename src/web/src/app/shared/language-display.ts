const LANGUAGE_LABELS: Record<string, string> = { bg: 'Български', en: 'English' };

/** A language code's display label — falls back to the upper-cased code for anything not in the known set. */
export function languageDisplayLabel(code: string): string {
  return LANGUAGE_LABELS[code] ?? code.toUpperCase();
}
