import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogApiService } from '../../../core/api/catalog-api.service';
import type { AcquisitionMethod, BookFormat, CoverType, Edition, LibraryItem, OwnershipStatus, WorkDetail } from '../../../core/api/models';
import { GenrePickerComponent } from '../../../shared/ui/genre-picker/genre-picker.component';
import { LibraryApiService } from '../library-api.service';

interface WorkFormControls {
  title: FormControl<string>;
  originalTitle: FormControl<string>;
  description: FormControl<string>;
  firstPublicationYear: FormControl<number | null>;
  authorNames: FormControl<string>;
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

interface AcquisitionFormControls {
  status: FormControl<OwnershipStatus>;
  acquiredOn: FormControl<string>;
  acquisitionMethod: FormControl<AcquisitionMethod>;
  priceAmount: FormControl<number | null>;
  currencyCode: FormControl<string>;
  source: FormControl<string>;
}

@Component({
  selector: 'app-library-edit-page',
  imports: [ReactiveFormsModule, TranslocoPipe, RouterLink, GenrePickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library-edit.page.html',
  styleUrl: './library-edit.page.scss',
})
export class LibraryEditPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LibraryApiService);
  private readonly catalog = inject(CatalogApiService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly statuses: readonly OwnershipStatus[] = ['Owned', 'Borrowed', 'LentOut', 'Sold', 'GivenAway'];
  protected readonly acquisitionMethods: readonly AcquisitionMethod[] = ['Bought', 'Gift', 'Borrowed', 'Inherited', 'Downloaded'];
  protected readonly coverTypes: readonly CoverType[] = ['Unknown', 'Hardcover', 'Paperback'];

  protected readonly itemResource = httpResource<LibraryItem | null>(() => `/api/v1/library-items/${this.id}`, {
    defaultValue: null,
  });

  protected readonly workResource = httpResource<WorkDetail | null>(
    () => {
      const item = this.itemResource.value();
      return item ? `/api/v1/works/${item.workId}` : undefined;
    },
    { defaultValue: null },
  );

  protected readonly editionResource = httpResource<Edition | null>(
    () => {
      const item = this.itemResource.value();
      return item ? `/api/v1/editions/${item.editionId}` : undefined;
    },
    { defaultValue: null },
  );

  protected readonly format = signal<BookFormat>('Physical');
  protected readonly selectedGenres = signal<readonly string[]>([]);

  protected readonly savingWork = signal(false);
  protected readonly workSaved = signal(false);
  protected readonly savingEdition = signal(false);
  protected readonly editionSaved = signal(false);
  protected readonly savingAcquisition = signal(false);
  protected readonly acquisitionSaved = signal(false);

  private workInitialized = false;
  private editionInitialized = false;
  private acquisitionInitialized = false;

  protected readonly workForm = new FormGroup<WorkFormControls>({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    originalTitle: new FormControl('', { nonNullable: true }),
    description: new FormControl('', { nonNullable: true }),
    firstPublicationYear: new FormControl<number | null>(null),
    authorNames: new FormControl('', { nonNullable: true }),
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

  protected readonly acquisitionForm = new FormGroup<AcquisitionFormControls>({
    status: new FormControl<OwnershipStatus>('Owned', { nonNullable: true }),
    acquiredOn: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    acquisitionMethod: new FormControl<AcquisitionMethod>('Bought', { nonNullable: true }),
    priceAmount: new FormControl<number | null>(null),
    currencyCode: new FormControl('BGN', { nonNullable: true }),
    source: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const work = this.workResource.value();
      if (work && !this.workInitialized) {
        this.workInitialized = true;
        this.workForm.setValue({
          title: work.title,
          originalTitle: work.originalTitle ?? '',
          description: work.description ?? '',
          firstPublicationYear: work.firstPublicationYear,
          authorNames: work.authors.map((a) => a.fullName).join(', '),
        });
        this.selectedGenres.set(work.genreNames);
      }
    });

    effect(() => {
      const edition = this.editionResource.value();
      if (edition && !this.editionInitialized) {
        this.editionInitialized = true;
        this.format.set(edition.format);
        this.editionForm.setValue({
          isbn13: edition.isbn13 ?? '',
          publisher: edition.publisher ?? '',
          language: edition.language ?? '',
          translator: edition.translator ?? '',
          publicationYear: edition.publicationYear,
          pageCount: edition.pageCount,
          coverType: edition.coverType,
          narrator: edition.narrator ?? '',
          durationMinutes: edition.durationMinutes,
        });
      }
    });

    effect(() => {
      const item = this.itemResource.value();
      if (item && !this.acquisitionInitialized) {
        this.acquisitionInitialized = true;
        this.acquisitionForm.setValue({
          status: item.status,
          acquiredOn: item.acquisition.acquiredOn,
          acquisitionMethod: item.acquisition.method,
          priceAmount: item.acquisition.price?.amount ?? null,
          currencyCode: item.acquisition.price?.currencyCode ?? 'BGN',
          source: item.acquisition.source ?? '',
        });
      }
    });
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
        originalTitle: raw.originalTitle || null,
        description: raw.description || null,
        firstPublicationYear: raw.firstPublicationYear,
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

  protected saveEdition(): void {
    const edition = this.editionResource.value();
    if (!edition || this.savingEdition()) return;

    const raw = this.editionForm.getRawValue();
    this.savingEdition.set(true);
    this.catalog
      .updateEdition(edition.id, {
        isbn13: raw.isbn13 || null,
        publisher: raw.publisher || null,
        language: raw.language || null,
        translator: raw.translator || null,
        publicationYear: raw.publicationYear,
        pageCount: this.format() === 'Audiobook' ? null : raw.pageCount,
        coverType: this.format() === 'Physical' ? raw.coverType : 'Unknown',
        narrator: this.format() === 'Audiobook' ? raw.narrator || null : null,
        durationMinutes: this.format() === 'Audiobook' ? raw.durationMinutes : null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingEdition.set(false);
          this.editionSaved.set(true);
        },
        error: () => this.savingEdition.set(false),
      });
  }

  protected saveAcquisition(): void {
    const item = this.itemResource.value();
    if (!item || this.acquisitionForm.invalid || this.savingAcquisition()) return;

    const raw = this.acquisitionForm.getRawValue();
    this.savingAcquisition.set(true);
    this.api
      .updateNote(
        this.id,
        {
          acquiredOn: raw.acquiredOn,
          method: raw.acquisitionMethod,
          price: raw.priceAmount != null ? { amount: raw.priceAmount, currencyCode: raw.currencyCode || 'BGN' } : null,
          source: raw.source || null,
        },
        item.personalNote,
      )
      .pipe(
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => this.applyStatusThenFinish(item.status, raw.status),
        error: () => this.savingAcquisition.set(false),
      });
  }

  private applyStatusThenFinish(currentStatus: OwnershipStatus, nextStatus: OwnershipStatus): void {
    if (currentStatus === nextStatus) {
      this.savingAcquisition.set(false);
      this.acquisitionSaved.set(true);
      return;
    }

    this.api
      .updateStatus(this.id, nextStatus)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingAcquisition.set(false);
          this.acquisitionSaved.set(true);
        },
        error: () => this.savingAcquisition.set(false),
      });
  }

  protected done(): void {
    this.router.navigate(['/library', this.id]);
  }
}
