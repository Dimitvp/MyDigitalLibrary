import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  AcquisitionMethod,
  BookFormat,
  BookMetadataCandidate,
  CreateLibraryItemRequest,
} from '../../core/api/models';
import { LibraryApiService } from '../library/library-api.service';
import { ImportApiService } from './import-api.service';

interface SearchControls {
  query: FormControl<string>;
}

interface ReviewControls {
  workTitle: FormControl<string>;
  authorNames: FormControl<string>;
  format: FormControl<BookFormat>;
  publisher: FormControl<string>;
  publicationYear: FormControl<number | null>;
  pageCount: FormControl<number | null>;
  description: FormControl<string>;
  acquiredOn: FormControl<string>;
  acquisitionMethod: FormControl<AcquisitionMethod>;
  priceAmount: FormControl<number | null>;
  currencyCode: FormControl<string>;
  source: FormControl<string>;
}

@Component({
  selector: 'app-import-page',
  imports: [ReactiveFormsModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './import.page.html',
  styleUrl: './import.page.scss',
})
export class ImportPage {
  private readonly importApi = inject(ImportApiService);
  private readonly libraryApi = inject(LibraryApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formats: readonly BookFormat[] = ['Physical', 'Ebook', 'Audiobook'];
  protected readonly acquisitionMethods: readonly AcquisitionMethod[] = ['Bought', 'Gift', 'Borrowed', 'Inherited', 'Downloaded'];

  protected readonly searching = signal(false);
  protected readonly submitting = signal(false);
  protected readonly searchError = signal<string | null>(null);
  protected readonly result = signal<{ isbn13: string; candidate: BookMetadataCandidate } | null>(null);

  protected readonly searchForm = new FormGroup<SearchControls>({
    query: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly reviewForm = new FormGroup<ReviewControls>({
    workTitle: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    authorNames: new FormControl('', { nonNullable: true }),
    format: new FormControl<BookFormat>('Physical', { nonNullable: true }),
    publisher: new FormControl('', { nonNullable: true }),
    publicationYear: new FormControl<number | null>(null),
    pageCount: new FormControl<number | null>(null),
    description: new FormControl('', { nonNullable: true }),
    acquiredOn: new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true, validators: [Validators.required] }),
    acquisitionMethod: new FormControl<AcquisitionMethod>('Bought', { nonNullable: true }),
    priceAmount: new FormControl<number | null>(null),
    currencyCode: new FormControl('BGN', { nonNullable: true }),
    source: new FormControl('', { nonNullable: true }),
  });

  protected search(): void {
    if (this.searchForm.invalid || this.searching()) {
      return;
    }

    const query = this.searchForm.getRawValue().query.trim();
    const isUrl = /^https?:\/\//i.test(query);

    this.searching.set(true);
    this.searchError.set(null);
    this.result.set(null);

    this.importApi
      .lookup(isUrl ? { url: query, isbn: null } : { url: null, isbn: query })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (lookup) => {
          this.searching.set(false);
          this.result.set({ isbn13: lookup.isbn13, candidate: lookup.candidate });
          this.prefillReviewForm(lookup.candidate);
        },
        error: () => {
          this.searching.set(false);
          this.searchError.set('import.search.notFound');
        },
      });
  }

  private prefillReviewForm(candidate: BookMetadataCandidate): void {
    this.reviewForm.patchValue({
      workTitle: candidate.title ?? '',
      authorNames: candidate.authorNames.join(', '),
      publisher: candidate.publisher ?? '',
      publicationYear: candidate.publicationYear,
      pageCount: candidate.pageCount,
      description: candidate.description ?? '',
    });
  }

  protected confirm(): void {
    const current = this.result();
    if (this.reviewForm.invalid || this.submitting() || !current) {
      return;
    }

    this.submitting.set(true);
    const raw = this.reviewForm.getRawValue();

    const request: CreateLibraryItemRequest = {
      editionId: null,
      work: {
        title: raw.workTitle,
        originalTitle: null,
        description: raw.description || null,
        firstPublicationYear: null,
        authorNames: raw.authorNames
          .split(',')
          .map((name) => name.trim())
          .filter((name) => name.length > 0),
        seriesName: null,
        seriesPosition: null,
        genreNames: null,
      },
      edition: {
        isbn13: current.isbn13,
        publisher: raw.publisher || null,
        language: null,
        translator: null,
        publicationYear: raw.publicationYear,
        pageCount: raw.pageCount,
        coverType: null,
        narrator: null,
        durationMinutes: null,
        coverUrl: current.candidate.coverUrl,
      },
      format: raw.format,
      status: null,
      acquisition: {
        acquiredOn: raw.acquiredOn,
        method: raw.acquisitionMethod,
        price: raw.priceAmount != null ? { amount: raw.priceAmount, currencyCode: raw.currencyCode || 'BGN' } : null,
        source: raw.source || null,
      },
      location: null,
    };

    this.libraryApi
      .create(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (item) => this.router.navigate(['/library', item.id]),
        error: () => this.submitting.set(false),
      });
  }

  protected startOver(): void {
    this.result.set(null);
    this.searchError.set(null);
    this.searchForm.reset({ query: '' });
  }
}
