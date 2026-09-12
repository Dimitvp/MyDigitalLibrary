import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { CatalogApiService } from '../../../core/api/catalog-api.service';
import type { Genre } from '../../../core/api/models';
import { LanguageService } from '../../../core/i18n/language.service';
import { genreDisplayName } from '../../genre-display';

/**
 * Checkbox list of genres, shared by the add/edit book forms and the detail
 * page's category section. Selection is tracked by name (matching the
 * CreateWorkInput/UpdateWorkRequest.genreNames contract) rather than id, so
 * a brand-new name typed into the "add" box can be selected immediately —
 * it's only actually created server-side (resolve-or-create) once the
 * parent form saves. Rename/delete act on the shared genre list right away
 * since those hit their own endpoints independent of the parent's save.
 */
@Component({
  selector: 'app-genre-picker',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './genre-picker.component.html',
  styleUrl: './genre-picker.component.scss',
})
export class GenrePickerComponent {
  private readonly catalog = inject(CatalogApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly language = inject(LanguageService);

  readonly selected = model.required<readonly string[]>();

  protected readonly genresResource = httpResource<Genre[]>(() => '/api/v1/genres', { defaultValue: [] });

  protected readonly newGenreName = signal('');
  protected readonly renamingId = signal<string | null>(null);
  protected readonly renameDraft = signal('');
  protected readonly renameDraftEn = signal('');

  protected isKnownGenre(name: string): boolean {
    return this.genresResource.value().some((g) => g.name === name);
  }

  protected label(genre: Genre): string {
    return genreDisplayName(genre, this.language.activeLangSignal());
  }

  protected toggle(name: string, checked: boolean): void {
    this.selected.update((current) => (checked ? [...current, name] : current.filter((g) => g !== name)));
  }

  protected addNew(): void {
    const name = this.newGenreName().trim();
    if (!name || this.selected().includes(name)) {
      this.newGenreName.set('');
      return;
    }

    this.selected.update((current) => [...current, name]);
    this.newGenreName.set('');
  }

  protected startRename(genre: Genre): void {
    this.renamingId.set(genre.id);
    this.renameDraft.set(genre.name);
    this.renameDraftEn.set(genre.nameEn ?? '');
  }

  protected cancelRename(): void {
    this.renamingId.set(null);
  }

  protected confirmRename(genre: Genre): void {
    const newName = this.renameDraft().trim();
    const newNameEn = this.renameDraftEn().trim() || null;
    if (!newName) {
      this.renamingId.set(null);
      return;
    }
    if (newName === genre.name && newNameEn === genre.nameEn) {
      this.renamingId.set(null);
      return;
    }

    this.catalog
      .renameGenre(genre.id, newName, newNameEn)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.renamingId.set(null);
          // The selection tracks names, not ids — carry a renamed, currently
          // selected genre's new name forward so the pending form doesn't
          // silently lose the selection.
          this.selected.update((current) => current.map((name) => (name === genre.name ? newName : name)));
          this.genresResource.reload();
        },
      });
  }

  protected deleteGenre(genre: Genre): void {
    if (!window.confirm(this.transloco.translate('library.genres.deleteConfirm'))) {
      return;
    }

    this.catalog
      .deleteGenre(genre.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.selected.update((current) => current.filter((name) => name !== genre.name));
          this.genresResource.reload();
        },
      });
  }
}
