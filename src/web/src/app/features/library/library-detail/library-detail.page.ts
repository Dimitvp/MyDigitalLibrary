import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { CatalogApiService } from '../../../core/api/catalog-api.service';
import type { LibraryItem, ReadingStatus, WorkDetail } from '../../../core/api/models';
import { GenrePickerComponent } from '../../../shared/ui/genre-picker/genre-picker.component';
import { LibraryApiService } from '../library-api.service';
import { ReadingApiService } from '../reading-api.service';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

@Component({
  selector: 'app-library-detail-page',
  imports: [RouterLink, TranslocoPipe, GenrePickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library-detail.page.html',
  styleUrl: './library-detail.page.scss',
})
export class LibraryDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LibraryApiService);
  private readonly reading = inject(ReadingApiService);
  private readonly catalog = inject(CatalogApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly itemResource = httpResource<LibraryItem | null>(() => `/api/v1/library-items/${this.id}`, {
    defaultValue: null,
  });

  protected readonly deleting = signal(false);

  protected readonly noteDraft = signal('');
  protected readonly savingNote = signal(false);
  protected readonly noteSaved = signal(false);
  private noteInitialized = false;

  // Mirrors the item's reading fields locally so Start/Finish can update the
  // UI immediately without re-fetching the whole library item.
  protected readonly readingStatus = signal<ReadingStatus | null>(null);
  protected readonly readingStartedOn = signal<string | null>(null);
  protected readonly readingEndedOn = signal<string | null>(null);
  protected readonly activeSessionId = signal<string | null>(null);
  protected readonly startDateInput = signal(today());
  protected readonly endDateInput = signal(today());
  protected readonly readingBusy = signal(false);
  private readingInitialized = false;

  // Cover replace — mirrors coverImageUrl locally so a successful upload shows immediately.
  protected readonly coverImageUrl = signal<string | null>(null);
  protected readonly uploadingCover = signal(false);
  private coverInitialized = false;

  // Genre correction — needs the full work (title/description/year) so saving
  // genres never clobbers fields this page doesn't otherwise show.
  protected readonly workResource = httpResource<WorkDetail | null>(
    () => {
      const item = this.itemResource.value();
      return item ? `/api/v1/works/${item.workId}` : undefined;
    },
    { defaultValue: null },
  );
  protected readonly selectedGenres = signal<readonly string[]>([]);
  protected readonly savingGenres = signal(false);
  protected readonly genresSaved = signal(false);
  private genresInitialized = false;

  constructor() {
    // Seed the editable draft once from the loaded item, without clobbering
    // in-progress typing on unrelated resource re-reads.
    effect(() => {
      const item = this.itemResource.value();
      if (item && !this.noteInitialized) {
        this.noteInitialized = true;
        this.noteDraft.set(item.personalNote ?? '');
      }
    });

    effect(() => {
      const item = this.itemResource.value();
      if (item && !this.readingInitialized) {
        this.readingInitialized = true;
        this.readingStatus.set(item.readingStatus);
        this.readingStartedOn.set(item.readingStartedOn);
        this.readingEndedOn.set(item.readingEndedOn);

        if (item.readingStatus === 'Reading') {
          this.reading
            .list(item.id)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe((sessions) => {
              const active = sessions.find((s) => s.status === 'Reading');
              if (active) this.activeSessionId.set(active.id);
            });
        }
      }

      if (item && !this.coverInitialized) {
        this.coverInitialized = true;
        this.coverImageUrl.set(item.coverImageUrl);
      }
    });

    effect(() => {
      const work = this.workResource.value();
      if (work && !this.genresInitialized) {
        this.genresInitialized = true;
        this.selectedGenres.set(work.genreNames);
      }
    });
  }

  protected onNoteInput(value: string): void {
    this.noteDraft.set(value);
    this.noteSaved.set(false);
  }

  protected saveNote(): void {
    const item = this.itemResource.value();
    if (!item || this.savingNote()) {
      return;
    }

    this.savingNote.set(true);
    this.api
      .updateNote(this.id, item.acquisition, this.noteDraft().trim() || null)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingNote.set(false);
          this.noteSaved.set(true);
        },
        error: () => this.savingNote.set(false),
      });
  }

  protected startReading(): void {
    if (this.readingBusy()) return;
    this.readingBusy.set(true);

    this.reading
      .start(this.id, this.startDateInput())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          this.readingBusy.set(false);
          this.readingStatus.set('Reading');
          this.readingStartedOn.set(session.startedOn);
          this.readingEndedOn.set(null);
          this.activeSessionId.set(session.id);
          this.endDateInput.set(today());
        },
        error: () => this.readingBusy.set(false),
      });
  }

  protected finishReading(): void {
    const sessionId = this.activeSessionId();
    if (!sessionId || this.readingBusy()) return;
    this.readingBusy.set(true);

    this.reading
      .finish(sessionId, this.endDateInput())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          this.readingBusy.set(false);
          this.readingStatus.set('Finished');
          this.readingEndedOn.set(session.endedOn);
          this.activeSessionId.set(null);
        },
        error: () => this.readingBusy.set(false),
      });
  }

  protected onCoverSelected(input: HTMLInputElement): void {
    const file = input.files?.[0];
    const item = this.itemResource.value();
    if (!file || !item || this.uploadingCover()) return;

    this.uploadingCover.set(true);
    this.catalog
      .uploadCover(item.editionId, file)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.uploadingCover.set(false);
          this.coverImageUrl.set(res.coverImageUrl);
          input.value = '';
        },
        error: () => this.uploadingCover.set(false),
      });
  }

  protected onGenresChange(names: readonly string[]): void {
    this.selectedGenres.set(names);
    this.genresSaved.set(false);
  }

  protected saveGenres(): void {
    const work = this.workResource.value();
    if (!work || this.savingGenres()) return;

    this.savingGenres.set(true);
    this.catalog
      .updateWork(work.id, {
        title: work.title,
        originalTitle: work.originalTitle,
        description: work.description,
        firstPublicationYear: work.firstPublicationYear,
        genreNames: this.selectedGenres(),
        authorNames: null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingGenres.set(false);
          this.genresSaved.set(true);
        },
        error: () => this.savingGenres.set(false),
      });
  }

  protected delete(): void {
    if (this.deleting()) {
      return;
    }

    if (!window.confirm(this.transloco.translate('library.detail.deleteConfirm'))) {
      return;
    }

    this.deleting.set(true);

    this.api
      .delete(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.router.navigateByUrl('/library'),
        error: () => this.deleting.set(false),
      });
  }
}
