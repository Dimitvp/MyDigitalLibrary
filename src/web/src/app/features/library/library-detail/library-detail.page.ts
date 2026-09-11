import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { LibraryItem } from '../../../core/api/models';
import { LibraryApiService } from '../library-api.service';

@Component({
  selector: 'app-library-detail-page',
  imports: [RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library-detail.page.html',
  styleUrl: './library-detail.page.scss',
})
export class LibraryDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LibraryApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly id = this.route.snapshot.paramMap.get('id')!;

  protected readonly itemResource = httpResource<LibraryItem | null>(() => `/api/v1/library-items/${this.id}`, {
    defaultValue: null,
  });

  protected readonly deleting = signal(false);

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
