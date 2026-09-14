import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Observable, map, of, switchMap, throwError } from 'rxjs';
import { CatalogApiService } from '../../../core/api/catalog-api.service';
import type { BookFormat, CoverType, Edition, WishlistEntry, WorkDetail } from '../../../core/api/models';
import { GenrePickerComponent } from '../../../shared/ui/genre-picker/genre-picker.component';
import { WishlistApiService } from '../wishlist-api.service';

interface WorkFormControls {
  title: FormControl<string>;
  authorNames: FormControl<string>;
}

interface WishFormControls {
  desiredFormat: FormControl<BookFormat>;
  priority: FormControl<number>;
  priceAmount: FormControl<number | null>;
  currencyCode: FormControl<string>;
  note: FormControl<string>;
  isOutOfStock: FormControl<boolean>;
}

interface EditionFormControls {
  isbn13: FormControl<string>;
  publisher: FormControl<string>;
  language: FormControl<string>;
  translator: FormControl<string>;
  publicationYear: FormControl<number | null>;
  pageCount: FormControl<number | null>;
  coverType: FormControl<CoverType>;
  narrator: FormControl<string>;
  durationMinutes: FormControl<number | null>;
}

@Component({
  selector: 'app-wishlist-edit-page',
  imports: [ReactiveFormsModule, TranslocoPipe, RouterLink, GenrePickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-edit.page.html',
  styleUrl: './wishlist-edit.page.scss',
})
export class WishlistEditPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(WishlistApiService);
  private readonly catalog = inject(CatalogApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly formats: readonly BookFormat[] = ['Physical', 'Ebook', 'Audiobook'];
  protected readonly priorities = [1, 2, 3, 4, 5];
  protected readonly coverTypes: readonly CoverType[] = ['Unknown', 'Hardcover', 'Paperback'];

  protected readonly entryResource = httpResource<WishlistEntry | null>(() => `/api/v1/wishlist/${this.id}`, {
    defaultValue: null,
  });

  protected readonly workResource = httpResource<WorkDetail | null>(
    () => {
      const entry = this.entryResource.value();
      return entry ? `/api/v1/works/${entry.workId}` : undefined;
    },
    { defaultValue: null },
  );

  protected readonly editionResource = httpResource<Edition | null>(
    () => {
      const entry = this.entryResource.value();
      return entry?.preferredEditionId ? `/api/v1/editions/${entry.preferredEditionId}` : undefined;
    },
    { defaultValue: null },
  );

  // Wishes may not have a preferred edition yet, so there's no fixed format
  // to read until one exists — fall back to the desired format so the
  // edition fields (ISBN vs narrator/duration, etc.) still show correctly.
  protected readonly format = computed<BookFormat>(
    () => this.editionResource.value()?.format ?? this.entryResource.value()?.desiredFormat ?? 'Physical',
  );

  protected readonly coverImageUrl = signal<string | null>(null);
  protected readonly uploadingCover = signal(false);
  protected readonly selectedGenres = signal<readonly string[]>([]);
  protected readonly deleting = signal(false);
  protected readonly savingWork = signal(false);
  protected readonly workSaved = signal(false);
  protected readonly savingWish = signal(false);
  protected readonly wishSaved = signal(false);
  protected readonly savingEdition = signal(false);
  protected readonly editionSaved = signal(false);

  private workInitialized = false;
  private wishInitialized = false;
  private coverInitialized = false;
  private editionInitialized = false;

  protected readonly workForm = new FormGroup<WorkFormControls>({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    authorNames: new FormControl('', { nonNullable: true }),
  });

  protected readonly wishForm = new FormGroup<WishFormControls>({
    desiredFormat: new FormControl<BookFormat>('Physical', { nonNullable: true }),
    priority: new FormControl(3, { nonNullable: true, validators: [Validators.required] }),
    priceAmount: new FormControl<number | null>(null),
    currencyCode: new FormControl('BGN', { nonNullable: true }),
    note: new FormControl('', { nonNullable: true }),
    isOutOfStock: new FormControl(false, { nonNullable: true }),
  });

  protected readonly editionForm = new FormGroup<EditionFormControls>({
    isbn13: new FormControl('', { nonNullable: true }),
    publisher: new FormControl('', { nonNullable: true }),
    language: new FormControl('', { nonNullable: true }),
    translator: new FormControl('', { nonNullable: true }),
    publicationYear: new FormControl<number | null>(null),
    pageCount: new FormControl<number | null>(null),
    coverType: new FormControl<CoverType>('Unknown', { nonNullable: true }),
    narrator: new FormControl('', { nonNullable: true }),
    durationMinutes: new FormControl<number | null>(null),
  });

  constructor() {
    effect(() => {
      const work = this.workResource.value();
      if (work && !this.workInitialized) {
        this.workInitialized = true;
        this.workForm.setValue({
          title: work.title,
          authorNames: work.authors.map((a) => a.fullName).join(', '),
        });
        this.selectedGenres.set(work.genreNames);
      }
    });

    effect(() => {
      const entry = this.entryResource.value();
      if (entry && !this.wishInitialized) {
        this.wishInitialized = true;
        this.wishForm.setValue({
          desiredFormat: entry.desiredFormat,
          priority: entry.priority,
          priceAmount: entry.maxPrice?.amount ?? null,
          currencyCode: entry.maxPrice?.currencyCode ?? 'BGN',
          note: entry.note ?? '',
          isOutOfStock: entry.isOutOfStock,
        });
      }
      if (entry && !this.coverInitialized) {
        this.coverInitialized = true;
        this.coverImageUrl.set(entry.coverImageUrl);
      }
      // No preferred edition yet — leave the edition form at its defaults
      // rather than waiting forever on editionResource to populate it.
      if (entry && !entry.preferredEditionId && !this.editionInitialized) {
        this.editionInitialized = true;
      }
    });

    effect(() => {
      const edition = this.editionResource.value();
      if (edition && !this.editionInitialized) {
        this.editionInitialized = true;
        this.editionForm.setValue({
          isbn13: edition.isbn13 ?? '',
          publisher: edition.publisher ?? '',
          language: edition.language === 'en' ? 'en' : 'bg',
          translator: edition.translator ?? '',
          publicationYear: edition.publicationYear,
          pageCount: edition.pageCount,
          coverType: edition.coverType,
          narrator: edition.narrator ?? '',
          durationMinutes: edition.durationMinutes,
        });
      }
    });
  }

  protected priorityLabel(priority: number): string {
    return this.transloco.translate(`wishlist.priorityLevel.${priority}`);
  }

  protected onGenresChange(names: readonly string[]): void {
    this.selectedGenres.set(names);
    this.workSaved.set(false);
  }

  protected saveWork(): void {
    const work = this.workResource.value();
    if (!work || this.workForm.invalid || this.savingWork()) return;

    const raw = this.workForm.getRawValue();
    this.savingWork.set(true);
    this.catalog
      .updateWork(work.id, {
        title: raw.title,
        originalTitle: work.originalTitle,
        description: work.description,
        firstPublicationYear: work.firstPublicationYear,
        genreNames: this.selectedGenres(),
        authorNames: raw.authorNames
          .split(',')
          .map((name) => name.trim())
          .filter((name) => name.length > 0),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingWork.set(false);
          this.workSaved.set(true);
        },
        error: () => this.savingWork.set(false),
      });
  }

  protected saveWish(): void {
    const entry = this.entryResource.value();
    if (!entry || this.wishForm.invalid || this.savingWish()) return;

    const raw = this.wishForm.getRawValue();
    this.savingWish.set(true);
    this.api
      .update(this.id, {
        desiredFormat: raw.desiredFormat,
        priority: raw.priority,
        preferredEditionId: entry.preferredEditionId,
        maxPrice: raw.priceAmount != null ? { amount: raw.priceAmount, currencyCode: raw.currencyCode || 'BGN' } : null,
        note: raw.note || null,
        isOutOfStock: raw.isOutOfStock,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingWish.set(false);
          this.wishSaved.set(true);
        },
        error: () => this.savingWish.set(false),
      });
  }

  private linkEdition(editionId: string): Observable<WishlistEntry> {
    const entry = this.entryResource.value()!;
    return this.api.update(this.id, {
      desiredFormat: entry.desiredFormat,
      priority: entry.priority,
      preferredEditionId: editionId,
      maxPrice: entry.maxPrice,
      note: entry.note,
      isOutOfStock: entry.isOutOfStock,
    });
  }

  // Wishlist entries only get an edition once the user attaches a cover or
  // fills in edition details — until then preferredEditionId is null, so
  // both actions need to lazily create one on first use.
  private ensureEditionId(): Observable<string> {
    const entry = this.entryResource.value();
    if (!entry) return throwError(() => new Error('Wishlist entry not loaded'));
    if (entry.preferredEditionId) return of(entry.preferredEditionId);

    return this.catalog
      .createEdition(entry.workId, {
        format: entry.desiredFormat,
        isbn13: null,
        publisher: null,
        language: null,
        translator: null,
        publicationYear: null,
        pageCount: null,
        coverType: null,
        narrator: null,
        durationMinutes: null,
        coverUrl: null,
      })
      .pipe(switchMap((created) => this.linkEdition(created.id).pipe(map(() => created.id))));
  }

  protected onCoverSelected(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file || this.uploadingCover()) return;

    this.uploadingCover.set(true);
    this.ensureEditionId()
      .pipe(
        switchMap((editionId) => this.catalog.uploadCover(editionId, file)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (res) => {
          this.uploadingCover.set(false);
          this.coverImageUrl.set(res.coverImageUrl);
          input.value = '';
          this.entryResource.reload();
        },
        error: () => this.uploadingCover.set(false),
      });
  }

  protected saveEdition(): void {
    const entry = this.entryResource.value();
    if (!entry || this.savingEdition()) return;

    const raw = this.editionForm.getRawValue();
    const isAudiobook = this.format() === 'Audiobook';
    const payload = {
      isbn13: isAudiobook ? null : raw.isbn13 || null,
      publisher: raw.publisher || null,
      language: raw.language || null,
      translator: raw.translator || null,
      publicationYear: raw.publicationYear,
      pageCount: isAudiobook ? null : raw.pageCount,
      coverType: this.format() === 'Physical' ? raw.coverType : ('Unknown' as CoverType),
      narrator: isAudiobook ? raw.narrator || null : null,
      durationMinutes: isAudiobook ? raw.durationMinutes : null,
    };

    this.savingEdition.set(true);

    const save$: Observable<unknown> = entry.preferredEditionId
      ? this.catalog.updateEdition(entry.preferredEditionId, payload)
      : this.catalog
          .createEdition(entry.workId, { format: entry.desiredFormat, ...payload, coverUrl: null })
          .pipe(switchMap((created) => this.linkEdition(created.id)));

    save$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.savingEdition.set(false);
        this.editionSaved.set(true);
        this.entryResource.reload();
      },
      error: () => this.savingEdition.set(false),
    });
  }

  protected delete(): void {
    if (this.deleting()) return;
    if (!window.confirm(this.transloco.translate('wishlist.list.deleteConfirm'))) return;

    this.deleting.set(true);
    this.api
      .delete(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.router.navigateByUrl('/wishlist'),
        error: () => this.deleting.set(false),
      });
  }

  protected done(): void {
    this.router.navigateByUrl('/wishlist');
  }
}
