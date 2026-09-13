import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogApiService } from '../../../core/api/catalog-api.service';
import type { AcquisitionMethod, WishlistEntry } from '../../../core/api/models';
import { WishlistApiService } from '../wishlist-api.service';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

interface FulfillFormControls {
  acquiredOn: FormControl<string>;
  acquisitionMethod: FormControl<AcquisitionMethod>;
  priceAmount: FormControl<number | null>;
  currencyCode: FormControl<string>;
  source: FormControl<string>;
  room: FormControl<string>;
  shelf: FormControl<string>;
  box: FormControl<string>;
}

@Component({
  selector: 'app-wishlist-fulfill-page',
  imports: [ReactiveFormsModule, TranslocoPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-fulfill.page.html',
  styleUrl: './wishlist-fulfill.page.scss',
})
export class WishlistFulfillPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(WishlistApiService);
  private readonly catalog = inject(CatalogApiService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly acquisitionMethods: readonly AcquisitionMethod[] = ['Bought', 'Gift', 'Borrowed', 'Inherited', 'Downloaded'];

  protected readonly entryResource = httpResource<WishlistEntry | null>(() => `/api/v1/wishlist/${this.id}`, {
    defaultValue: null,
  });

  protected readonly submitting = signal(false);

  protected readonly form = new FormGroup<FulfillFormControls>({
    acquiredOn: new FormControl(today(), { nonNullable: true, validators: [Validators.required] }),
    acquisitionMethod: new FormControl<AcquisitionMethod>('Bought', { nonNullable: true }),
    priceAmount: new FormControl<number | null>(null),
    currencyCode: new FormControl('BGN', { nonNullable: true }),
    source: new FormControl('', { nonNullable: true }),
    room: new FormControl('', { nonNullable: true }),
    shelf: new FormControl('', { nonNullable: true }),
    box: new FormControl('', { nonNullable: true }),
  });

  protected submit(): void {
    const entry = this.entryResource.value();
    if (!entry || this.form.invalid || this.submitting()) return;

    this.submitting.set(true);

    if (entry.preferredEditionId) {
      this.fulfillWith(entry, entry.preferredEditionId);
      return;
    }

    // No edition on file yet — create a bare one (no ISBN/cover) just so the
    // new LibraryItem has something to point at.
    this.catalog
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
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (edition) => this.fulfillWith(entry, edition.id),
        error: () => this.submitting.set(false),
      });
  }

  private fulfillWith(entry: WishlistEntry, editionId: string): void {
    const raw = this.form.getRawValue();
    const hasLocation = entry.desiredFormat === 'Physical' && (raw.room || raw.shelf || raw.box);

    this.api
      .fulfill(this.id, {
        editionId,
        acquisition: {
          acquiredOn: raw.acquiredOn,
          method: raw.acquisitionMethod,
          price: raw.priceAmount != null ? { amount: raw.priceAmount, currencyCode: raw.currencyCode || 'BGN' } : null,
          source: raw.source || null,
        },
        location: hasLocation ? { room: raw.room || null, shelf: raw.shelf || null, box: raw.box || null } : null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (item) => this.router.navigate(['/library', item.id]),
        error: () => this.submitting.set(false),
      });
  }
}
