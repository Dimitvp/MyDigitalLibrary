import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { AcquisitionMethod, BookFormat, CreateLibraryItemRequest } from '../../../core/api/models';
import { LibraryApiService } from '../library-api.service';

interface LibraryFormControls {
  workTitle: FormControl<string>;
  authorNames: FormControl<string>;
  format: FormControl<BookFormat>;
  isbn13: FormControl<string>;
  publisher: FormControl<string>;
  publicationYear: FormControl<number | null>;
  pageCount: FormControl<number | null>;
  acquiredOn: FormControl<string>;
  acquisitionMethod: FormControl<AcquisitionMethod>;
  priceAmount: FormControl<number | null>;
  currencyCode: FormControl<string>;
  source: FormControl<string>;
}

@Component({
  selector: 'app-library-form-page',
  imports: [ReactiveFormsModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library-form.page.html',
  styleUrl: './library-form.page.scss',
})
export class LibraryFormPage {
  private readonly api = inject(LibraryApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formats: readonly BookFormat[] = ['Physical', 'Ebook', 'Audiobook'];
  protected readonly acquisitionMethods: readonly AcquisitionMethod[] = ['Bought', 'Gift', 'Borrowed', 'Inherited', 'Downloaded'];

  protected readonly submitting = signal(false);

  protected readonly form = new FormGroup<LibraryFormControls>({
    workTitle: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    authorNames: new FormControl('', { nonNullable: true }),
    format: new FormControl<BookFormat>('Physical', { nonNullable: true }),
    isbn13: new FormControl('', { nonNullable: true }),
    publisher: new FormControl('', { nonNullable: true }),
    publicationYear: new FormControl<number | null>(null),
    pageCount: new FormControl<number | null>(null),
    acquiredOn: new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true, validators: [Validators.required] }),
    acquisitionMethod: new FormControl<AcquisitionMethod>('Bought', { nonNullable: true }),
    priceAmount: new FormControl<number | null>(null),
    currencyCode: new FormControl('BGN', { nonNullable: true }),
    source: new FormControl('', { nonNullable: true }),
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    const raw = this.form.getRawValue();

    const request: CreateLibraryItemRequest = {
      editionId: null,
      work: {
        title: raw.workTitle,
        originalTitle: null,
        description: null,
        firstPublicationYear: null,
        authorNames: raw.authorNames
          .split(',')
          .map((name) => name.trim())
          .filter((name) => name.length > 0),
        seriesName: null,
        seriesPosition: null,
      },
      edition: {
        isbn13: raw.isbn13 || null,
        publisher: raw.publisher || null,
        language: null,
        translator: null,
        publicationYear: raw.publicationYear,
        pageCount: raw.pageCount,
        coverType: null,
        narrator: null,
        durationMinutes: null,
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

    this.api
      .create(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (item) => this.router.navigate(['/library', item.id]),
        error: () => this.submitting.set(false),
      });
  }
}
