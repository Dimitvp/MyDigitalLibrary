# Services & State Management — Reference

## Service Anatomy

```typescript
@Injectable({ providedIn: 'root' })  // tree-shakeable, single instance
export class OrderService {
    // ✅ Inject dependencies with inject()
    private readonly http   = inject(HttpClient);
    private readonly router = inject(Router);
    private readonly api    = inject(API_BASE_URL);

    // ✅ Private state — not accessible outside the service
    private readonly _isLoading = signal(false);
    private readonly _error     = signal<string | null>(null);

    // ✅ Public read-only state
    readonly isLoading = this._isLoading.asReadonly();
    readonly error     = this._error.asReadonly();

    // ✅ Clean public API
    getAll(): Observable<Order[]> {
        return this.http.get<Order[]>(`${this.api}/orders`);
    }

    create(dto: CreateOrderDto): Observable<Order> {
        return this.http.post<Order>(`${this.api}/orders`, dto);
    }
}
```

---

## Feature State Service Pattern

```typescript
// Self-contained feature state — no NgRx needed for most features
@Injectable({ providedIn: 'root' })
export class CartService {
    private readonly http = inject(HttpClient);

    // State
    private readonly _items   = signal<CartItem[]>([]);
    private readonly _loading = signal(false);

    // Derived state
    readonly items     = this._items.asReadonly();
    readonly itemCount = computed(() => this._items().length);
    readonly subtotal  = computed(() =>
        this._items().reduce((sum, i) => sum + i.price * i.quantity, 0)
    );
    readonly tax       = computed(() => this.subtotal() * 0.2);
    readonly total     = computed(() => this.subtotal() + this.tax());
    readonly isEmpty   = computed(() => this._items().length === 0);

    // Commands
    addItem(product: Product): void {
        this._items.update(items => {
            const existing = items.find(i => i.productId === product.id);
            if (existing) {
                return items.map(i =>
                    i.productId === product.id
                        ? { ...i, quantity: i.quantity + 1 }
                        : i
                );
            }
            return [...items, { productId: product.id, name: product.name, price: product.price, quantity: 1 }];
        });
    }

    removeItem(productId: string): void {
        this._items.update(items => items.filter(i => i.productId !== productId));
    }

    async checkout(): Promise<void> {
        this._loading.set(true);
        try {
            await firstValueFrom(this.http.post('/api/orders', { items: this._items() }));
            this._items.set([]);
        } finally {
            this._loading.set(false);
        }
    }
}
```

---

## When to Use NgRx

Use NgRx when you have:
- State shared across **many** unrelated feature modules
- Complex state transitions requiring strict action history (undo/redo, debugging)
- Team conventions that require a Redux-style pattern
- Time-travel debugging requirement

**Don't use NgRx for:**
- State that belongs to a single feature
- Server cache (use a service + `shareReplay` or TanStack Query)
- Simple UI state (use signals)

```typescript
// NgRx signal store (modern NgRx approach)
import { signalStore, withState, withComputed, withMethods } from '@ngrx/signals';

export const ProductStore = signalStore(
    { providedIn: 'root' },
    withState<ProductState>({ products: [], loading: false, error: null }),
    withComputed(({ products }) => ({
        featuredProducts: computed(() => products().filter(p => p.isFeatured)),
        productCount:     computed(() => products().length)
    })),
    withMethods((store, productService = inject(ProductService)) => ({
        async loadProducts(): Promise<void> {
            patchState(store, { loading: true });
            const products = await firstValueFrom(productService.getAll());
            patchState(store, { products, loading: false });
        }
    }))
);
```

---

## Injection Scopes

```typescript
// providedIn: 'root' — singleton, available everywhere, tree-shakeable
@Injectable({ providedIn: 'root' })
export class AuthService { }

// providedIn: 'any' — new instance per lazy module (deprecated in newer Angular)
// Avoid — use component-level providers instead

// Component-level providers — new instance per component instance
@Component({
    providers: [FormStateService]  // new instance for each component
})
export class CheckoutComponent {
    private readonly formState = inject(FormStateService);
}

// Route-level providers — scoped to route lifetime
{
    path: 'checkout',
    component: CheckoutPageComponent,
    providers: [CheckoutStateService]  // destroyed when leaving the route
}
```
