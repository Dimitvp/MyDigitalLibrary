# Signals — Reference (Angular 16+)

## Core Signal API

```typescript
import { signal, computed, effect, toSignal, toObservable } from '@angular/core';

// signal() — writable reactive value
const count = signal(0);
count();              // read
count.set(5);         // set
count.update(c => c + 1);  // update based on current value
count.asReadonly();   // expose read-only version

// computed() — derived read-only signal, memoised
const doubled = computed(() => count() * 2);
const fullName = computed(() => `${firstName()} ${lastName()}`);

// effect() — side effect when signals change
effect(() => {
    console.log('Count changed:', count());  // auto-tracks count
    // cleanup function (optional)
    return () => console.log('cleanup');
});
```

---

## Signal-Based Service State

```typescript
@Injectable({ providedIn: 'root' })
export class ProductStateService {
    private readonly _products    = signal<Product[]>([]);
    private readonly _isLoading   = signal(false);
    private readonly _error       = signal<string | null>(null);

    // Expose read-only signals — encapsulation
    readonly products  = this._products.asReadonly();
    readonly isLoading = this._isLoading.asReadonly();
    readonly error     = this._error.asReadonly();

    // Derived state — auto-updated
    readonly productCount    = computed(() => this._products().length);
    readonly hasProducts     = computed(() => this._products().length > 0);
    readonly featuredProducts = computed(() =>
        this._products().filter(p => p.isFeatured)
    );

    constructor(private readonly http: HttpClient) {}

    loadProducts(): void {
        this._isLoading.set(true);
        this._error.set(null);

        this.http.get<Product[]>('/api/products').subscribe({
            next:  products => { this._products.set(products); this._isLoading.set(false); },
            error: err      => { this._error.set('Failed to load'); this._isLoading.set(false); }
        });
    }

    addProduct(product: Product): void {
        this._products.update(list => [...list, product]);
    }

    removeProduct(id: string): void {
        this._products.update(list => list.filter(p => p.id !== id));
    }
}
```

---

## toSignal() — Observable to Signal Bridge

```typescript
@Component({ standalone: true, template: `
    @if (products()) {
        @for (p of products(); track p.id) {
            <app-product-card [product]="p"/>
        }
    } @else {
        <app-spinner/>
    }
`})
export class ProductListComponent {
    private readonly productService = inject(ProductService);

    // ✅ toSignal — converts Observable to Signal for template use
    readonly products = toSignal(
        this.productService.getAll(),
        { initialValue: null }   // null while loading
    );

    // With error handling
    readonly products2 = toSignal(
        this.productService.getAll().pipe(catchError(() => of([]))),
        { initialValue: [] }
    );
}
```

---

## toObservable() — Signal to Observable Bridge

```typescript
// When you need RxJS operators on a signal's value
export class SearchComponent {
    searchTerm = signal('');

    // Bridge signal to Observable to use RxJS operators
    readonly results$ = toObservable(this.searchTerm).pipe(
        debounceTime(300),
        distinctUntilChanged(),
        filter(term => term.length >= 2),
        switchMap(term => this.searchService.search(term))
    );
}
```

---

## Signal Inputs (Angular 17.1+)

```typescript
import { input, output, model } from '@angular/core';

@Component({ standalone: true })
export class ProductCardComponent {
    // Signal input — read as product()
    product     = input.required<Product>();
    highlighted = input(false);  // with default

    // Signal output
    selected = output<Product>();

    // Two-way binding with model()
    quantity = model(1);

    // Computed from input signal
    protected displayPrice = computed(() =>
        `${this.product().price.toFixed(2)} ${this.product().currency}`
    );

    protected onSelect(): void {
        this.selected.emit(this.product());
    }
}

// Usage in template:
// <app-product-card [product]="item" [(quantity)]="qty" (selected)="onSelected($event)"/>
```

---

## Effect Rules and Pitfalls

```typescript
// ✅ Valid effect uses
effect(() => {
    // Side effects: localStorage, logging, third-party libs
    localStorage.setItem('theme', this.theme());
});

effect(() => {
    // Sync to external system
    this.analyticsService.track('page-view', this.currentPage());
});

// ❌ Don't use effect to sync signals — use computed instead
effect(() => {
    this.fullName.set(`${this.first()} ${this.last()}`);  // ← wrong
});
// ✅ Fix:
fullName = computed(() => `${this.first()} ${this.last()}`);

// ❌ Don't write to signals inside effect without allowSignalWrites
effect(() => {
    if (this.count() > 10) {
        this.count.set(0);  // ← causes infinite loop warning
    }
});
// ✅ Fix: use allowSignalWrites: true carefully, or restructure logic
effect(() => {
    if (this.count() > 10) {
        this.count.set(0);
    }
}, { allowSignalWrites: true });
```
