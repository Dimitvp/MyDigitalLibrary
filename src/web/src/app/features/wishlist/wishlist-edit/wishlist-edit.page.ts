import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { CatalogApiService } from '../../../core/api/catalog-api.service';
import type { BookFormat, ImportLookupResult, WishlistEntry, WorkDetail } from '../../../core/api/models';
import { ImportApiService } from '../../import/import-api.service';
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

@Component({
  selector: 'app-wishlist-edit-page',
  imports: [ReactiveFormsModule, TranslocoPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-edit.page.html',
  styleUrl: './wishlist-edit.page.scss',
})
export class WishlistEditPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(WishlistApiService);
  private readonly catalog = inject(CatalogApiService);
  private readonly importApi = inject(ImportApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly formats: readonly BookFormat[] = ['Physical', 'Ebook', 'Audiobook'];
  protected readonly priorities = [1, 2, 3, 4, 5];

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

  protected readonly coverImageUrl = signal<string | null>(null);
  protected readonly deleting = signal(false);
  protected readonly savingWork = signal(false);
  protected readonly workSaved = signal(false);
  protected readonly savingWish = signal(false);
  protected readonly wishSaved = signal(false);

  protected readonly coverSearching = signal(false);
  protected readonly coverSearchError = signal<string | null>(null);
  protected readonly coverCandidate = signal<ImportLookupResult | null>(null);
  protected readonly coverAttaching = signal(false);

  private workInitialized = false;
  private wishInitialized = false;
  private coverInitialized = false;

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

  constructor() {
    effect(() => {
      const work = this.workResource.value();
      if (work && !this.workInitialized) {
        this.workInitialized = true;
        this.workForm.setValue({
          title: work.title,
          authorNames: work.authors.map((a) => a.fullName).join(', '),
        });
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
    });
  }

  protected priorityLabel(priority: number): string {
    return this.transloco.translate(`wishlist.priorityLevel.${priority}`);
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
        genreNames: work.genreNames,
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

  protected searchCover(rawQuery: string): void {
    const query = rawQuery.trim();
    if (!query || this.coverSearching()) return;

    const isUrl = /^https?:\/\//i.test(query);
    this.coverSearching.set(true);
    this.coverSearchError.set(null);
    this.coverCandidate.set(null);

    this.importApi
      .lookup(isUrl ? { url: query, isbn: null } : { url: null, isbn: query })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.coverSearching.set(false);
          this.coverCandidate.set(result);
        },
        error: () => {
          this.coverSearching.set(false);
          this.coverSearchError.set('import.search.notFound');
        },
      });
  }

  protected useCoverCandidate(): void {
    const entry = this.entryResource.value();
    const found = this.coverCandidate();
    if (!entry || !found || this.coverAttaching()) return;

    this.coverAttaching.set(true);
    this.catalog
      .createEdition(entry.workId, {
        format: entry.desiredFormat,
        isbn13: found.isbn13,
        publisher: found.candidate.publisher,
        language: found.candidate.language,
        translator: null,
        publicationYear: found.candidate.publicationYear,
        pageCount: found.candidate.pageCount,
        coverType: null,
        narrator: null,
        durationMinutes: null,
        coverUrl: found.candidate.coverUrl,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (edition) => {
          this.api
            .update(this.id, {
              desiredFormat: entry.desiredFormat,
              priority: entry.priority,
              preferredEditionId: edition.id,
              maxPrice: entry.maxPrice,
              note: entry.note,
              isOutOfStock: entry.isOutOfStock,
            })
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.coverAttaching.set(false);
                this.coverImageUrl.set(found.candidate.coverUrl);
                this.coverCandidate.set(null);
              },
              error: () => this.coverAttaching.set(false),
            });
        },
        error: () => this.coverAttaching.set(false),
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
