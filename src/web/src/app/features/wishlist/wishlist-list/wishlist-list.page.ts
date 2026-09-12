import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { WishlistEntry } from '../../../core/api/models';

@Component({
  selector: 'app-wishlist-list-page',
  imports: [RouterLink, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-list.page.html',
  styleUrl: './wishlist-list.page.scss',
})
export class WishlistListPage {
  protected readonly listResource = httpResource<WishlistEntry[]>(() => '/api/v1/wishlist', {
    defaultValue: [],
  });
}
