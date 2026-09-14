import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { Edition, WishlistEntry } from '../../../core/api/models';
import { languageDisplayLabel } from '../../../shared/language-display';
import { WishlistApiService } from '../wishlist-api.service';

@Component({
  selector: 'app-wishlist-detail-page',
  imports: [RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-detail.page.html',
  styleUrl: './wishlist-detail.page.scss',
})
export class WishlistDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(WishlistApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly entryResource = httpResource<WishlistEntry | null>(() => `/api/v1/wishlist/${this.id}`, {
    defaultValue: null,
  });

  // A wish may not have a preferred edition yet (added without ISBN/language) — the
  // dl block below simply shows nothing for those fields until one exists.
  protected readonly editionResource = httpResource<Edition | null>(
    () => {
      const entry = this.entryResource.value();
      return entry?.preferredEditionId ? `/api/v1/editions/${entry.preferredEditionId}` : undefined;
    },
    { defaultValue: null },
  );

  protected readonly noteDraft = signal('');
  protected readonly savingNote = signal(false);
  protected readonly noteSaved = signal(false);
  private noteInitialized = false;

  protected readonly deleting = signal(false);

  constructor() {
    effect(() => {
      const entry = this.entryResource.value();
      if (entry && !this.noteInitialized) {
        this.noteInitialized = true;
        this.noteDraft.set(entry.note ?? '');
      }
    });
  }

  protected languageLabel(code: string | null): string | null {
    return code ? languageDisplayLabel(code) : null;
  }

  protected priorityLabel(priority: number): string {
    return this.transloco.translate(`wishlist.priorityLevel.${priority}`);
  }

  protected onNoteInput(value: string): void {
    this.noteDraft.set(value);
    this.noteSaved.set(false);
  }

  protected saveNote(): void {
    const entry = this.entryResource.value();
    if (!entry || this.savingNote()) {
      return;
    }

    this.savingNote.set(true);
    this.api
      .update(this.id, {
        desiredFormat: entry.desiredFormat,
        priority: entry.priority,
        preferredEditionId: entry.preferredEditionId,
        maxPrice: entry.maxPrice,
        note: this.noteDraft().trim() || null,
        isOutOfStock: entry.isOutOfStock,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingNote.set(false);
          this.noteSaved.set(true);
        },
        error: () => this.savingNote.set(false),
      });
  }

  protected delete(): void {
    if (this.deleting()) {
      return;
    }

    if (!window.confirm(this.transloco.translate('wishlist.list.deleteConfirm'))) {
      return;
    }

    this.deleting.set(true);
    this.api
      .delete(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.router.navigateByUrl('/wishlist'),
        error: () => this.deleting.set(false),
      });
  }
}
