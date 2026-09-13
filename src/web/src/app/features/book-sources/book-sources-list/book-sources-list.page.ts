import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import type { CreateFollowedBookSourceRequest, FollowedBookSource } from '../../../core/api/models';
import { BookSourcesApiService } from '../book-sources-api.service';

const NO_CATEGORY = '￿'; // sorts after every real category name

interface CategoryGroup {
  categoryKey: string;
  categoryLabel: string | null;
  sources: FollowedBookSource[];
}

interface BookSourceFormControls {
  name: FormControl<string>;
  url: FormControl<string>;
  category: FormControl<string>;
  notes: FormControl<string>;
}

@Component({
  selector: 'app-book-sources-list-page',
  imports: [ReactiveFormsModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './book-sources-list.page.html',
  styleUrl: './book-sources-list.page.scss',
})
export class BookSourcesListPage {
  private readonly api = inject(BookSourcesApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly submitting = signal(false);
  protected readonly formOpen = signal(false);

  protected readonly listResource = httpResource<FollowedBookSource[]>(() => '/api/v1/book-sources', {
    defaultValue: [],
  });

  protected readonly groups = computed<CategoryGroup[]>(() => {
    const sources = this.listResource.value();
    const byCategory = new Map<string, FollowedBookSource[]>();

    for (const source of sources) {
      const key = source.category ?? NO_CATEGORY;
      const bucket = byCategory.get(key);
      if (bucket) bucket.push(source);
      else byCategory.set(key, [source]);
    }

    const categoryKeys = [...byCategory.keys()].sort((a, b) => a.localeCompare(b));

    return categoryKeys.map((categoryKey) => ({
      categoryKey,
      categoryLabel: categoryKey === NO_CATEGORY ? null : categoryKey,
      sources: byCategory.get(categoryKey)!.sort((a, b) => a.name.localeCompare(b.name)),
    }));
  });

  protected readonly form = new FormGroup<BookSourceFormControls>({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    url: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    category: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true }),
  });

  protected toggleForm(): void {
    this.formOpen.update((open) => !open);
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    const raw = this.form.getRawValue();

    const request: CreateFollowedBookSourceRequest = {
      name: raw.name,
      url: raw.url,
      category: raw.category || null,
      notes: raw.notes || null,
    };

    this.api
      .create(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.form.reset({ name: '', url: '', category: '', notes: '' });
          this.formOpen.set(false);
          this.listResource.reload();
        },
        error: () => this.submitting.set(false),
      });
  }

  protected remove(id: string): void {
    this.api
      .delete(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.listResource.reload());
  }
}
