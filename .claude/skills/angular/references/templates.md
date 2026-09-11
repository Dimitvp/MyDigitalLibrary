# Templates — Reference

## Modern Control Flow (Angular 17+)

```html
<!-- @if / @else if / @else -->
@if (product.isAvailable) {
    <button (click)="addToCart(product)">Add to Cart</button>
} @else if (product.isComingSoon) {
    <button disabled>Coming Soon</button>
} @else {
    <span class="out-of-stock">Out of Stock</span>
}

<!-- @for with track (required) -->
@for (product of products; track product.id) {
    <app-product-card [product]="product" />
} @empty {
    <p class="empty-state">No products found.</p>
}

<!-- @switch -->
@switch (order.status) {
    @case ('pending')   { <app-pending-badge/> }
    @case ('confirmed') { <app-confirmed-badge/> }
    @case ('shipped')   { <app-shipped-badge/> }
    @default            { <app-unknown-badge/> }
}

<!-- @defer — lazy render heavy component -->
@defer (on viewport; prefetch on idle) {
    <app-heavy-analytics-chart [data]="chartData"/>
} @placeholder (minimum 200ms) {
    <div class="chart-placeholder skeleton"/>
} @loading (after 100ms; minimum 400ms) {
    <app-spinner/>
} @error {
    <p>Chart failed to load. <button (click)="retryChart()">Retry</button></p>
}
```

---

## Async Pipe Patterns

```html
<!-- ❌ Multiple async on same stream — creates multiple subscriptions -->
<div *ngIf="user$ | async">
    <p>{{ (user$ | async)?.name }}</p>
    <p>{{ (user$ | async)?.email }}</p>
</div>

<!-- ✅ Single subscription with as alias -->
@if (user$ | async; as user) {
    <p>{{ user.name }}</p>
    <p>{{ user.email }}</p>
}

<!-- ✅ combineLatest for multiple streams -->
@if (vm$ | async; as vm) {
    <app-product-list [products]="vm.products" [category]="vm.category"/>
}
<!-- In component: vm$ = combineLatest({ products: products$, category: category$ }) -->
```

---

## Custom Pipes

```typescript
// ✅ Pure pipe — cached, runs only when input reference changes
@Pipe({ name: 'truncate', pure: true, standalone: true })
export class TruncatePipe implements PipeTransform {
    transform(value: string, limit = 100, ellipsis = '...'): string {
        if (!value || value.length <= limit) return value;
        return value.substring(0, limit).trimEnd() + ellipsis;
    }
}

// ✅ Pipe for expensive template computation (replaces method calls)
@Pipe({ name: 'orderTotal', pure: true, standalone: true })
export class OrderTotalPipe implements PipeTransform {
    transform(lines: OrderLine[]): number {
        return lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
    }
}

// Usage in template:
// <p>Total: {{ order.lines | orderTotal | currency }}</p>
// ← runs only when order.lines reference changes, not every CD cycle
```

---

## ng-container and ng-template

```html
<!-- ng-container — grouping without extra DOM element -->
<ng-container *ngIf="isAdmin">
    <button>Edit</button>
    <button>Delete</button>
</ng-container>

<!-- ng-template — reusable template fragment -->
<ng-template #loadingTpl>
    <app-skeleton-loader [rows]="3"/>
</ng-template>

<ng-template #errorTpl let-message="message">
    <app-error-banner [message]="message"/>
</ng-template>

@if (isLoading) {
    <ng-container *ngTemplateOutlet="loadingTpl"/>
} @else if (error) {
    <ng-container *ngTemplateOutlet="errorTpl; context: { message: error }"/>
} @else {
    <app-product-list [products]="products"/>
}
```

---

## Template Variables and @let (Angular 18+)

```html
<!-- @let — declare local template variable -->
@let discountedPrice = product.price * (1 - product.discountRate);
@let isOnSale = product.discountRate > 0;

@if (isOnSale) {
    <span class="original-price">{{ product.price | currency }}</span>
    <span class="sale-price">{{ discountedPrice | currency }}</span>
}
```

---

## NgOptimizedImage (Angular 15+)

```html
<!-- ❌ Regular img — no lazy loading, no size hints, CLS issues -->
<img src="/assets/hero.jpg" alt="Hero image">

<!-- ✅ NgOptimizedImage — automatic lazy loading, preconnect, blur placeholder -->
<img
    ngSrc="/assets/hero.jpg"
    alt="Hero image"
    width="1200"
    height="600"
    priority              <!-- for LCP image — disables lazy loading -->
/>

<img
    ngSrc="{{ product.imageUrl }}"
    alt="{{ product.name }}"
    width="300"
    height="300"
    loading="lazy"
/>
```

---

## Host Binding and Listeners

```typescript
// ✅ Use host metadata in @Component decorator (preferred over @HostBinding)
@Component({
    selector: 'app-button',
    host: {
        'class':           'btn',
        '[class.btn-primary]': 'isPrimary',
        '[disabled]':          'isDisabled',
        '(click)':             'onClick()'
    }
})
export class ButtonComponent {
    @Input() isPrimary  = false;
    @Input() isDisabled = false;
    @Output() clicked   = new EventEmitter<void>();

    onClick(): void { if (!this.isDisabled) this.clicked.emit(); }
}
```
