import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { BookFormat, CreateWishlistEntryRequest } from '../../../core/api/models';
import { WishlistApiService } from '../wishlist-api.service';

interface WishlistFormControls {
  workTitle: FormControl<string>;
  authorNames: FormControl<string>;
  desiredFormat: FormControl<BookFormat>;
  priority: FormControl<number>;
  note: FormControl<string>;
}

@Component({
  selector: 'app-wishlist-form-page',
  imports: [ReactiveFormsModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-form.page.html',
  styleUrl: './wishlist-form.page.scss',
})
export class WishlistFormPage {
  private readonly api = inject(WishlistApiService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formats: readonly BookFormat[] = ['Physical', 'Ebook', 'Audiobook'];
  protected readonly priorities = [1, 2, 3, 4, 5];

  protected readonly submitting = signal(false);

  protected readonly form = new FormGroup<WishlistFormControls>({
    workTitle: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    authorNames: new FormControl('', { nonNullable: true }),
    desiredFormat: new FormControl<BookFormat>('Physical', { nonNullable: true }),
    priority: new FormControl(3, { nonNullable: true, validators: [Validators.required] }),
    note: new FormControl('', { nonNullable: true }),
  });

  protected priorityLabel(priority: number): string {
    return this.transloco.translate(`wishlist.priorityLevel.${priority}`);
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    const raw = this.form.getRawValue();

    const request: CreateWishlistEntryRequest = {
      workId: null,
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
        genreNames: null,
      },
      desiredFormat: raw.desiredFormat,
      priority: raw.priority,
      preferredEditionId: null,
      maxPrice: null,
      note: raw.note || null,
      isOutOfStock: false,
    };

    this.api
      .create(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.router.navigateByUrl('/wishlist'),
        error: () => this.submitting.set(false),
      });
  }
}
