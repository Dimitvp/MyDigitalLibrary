# RxJS Patterns — Reference

## Operator Selection Guide

```
What do you need?                           Use
─────────────────────────────────────────────────────────────
One inner observable at a time (cancel old) switchMap  ← HTTP searches
All inner observables concurrently          mergeMap   ← fire and forget
Queue inner observables in order            concatMap  ← sequential saves
Ignore new while inner is active            exhaustMap ← submit button
```

---

## Memory Leak Prevention

```typescript
// Option 1 — takeUntilDestroyed (Angular 16+, preferred)
export class MyComponent {
    ngOnInit() {
        this.service.getData()
            .pipe(takeUntilDestroyed(inject(DestroyRef)))
            .subscribe(data => this.data = data);
    }
}

// Option 2 — async pipe (best for template-bound data)
@Component({ template: `<div>{{ data$ | async }}</div>` })
export class MyComponent {
    readonly data$ = this.service.getData();  // no subscribe, no cleanup
}

// Option 3 — toSignal (Angular 16+, best with Signals)
export class MyComponent {
    readonly data = toSignal(this.service.getData(), { initialValue: null });
}

// ❌ Old Subject pattern — more boilerplate, still valid for complex cases
export class MyComponent implements OnDestroy {
    private readonly destroy$ = new Subject<void>();

    ngOnInit() {
        this.service.getData()
            .pipe(takeUntil(this.destroy$))
            .subscribe(data => this.data = data);
    }

    ngOnDestroy() { this.destroy$.next(); this.destroy$.complete(); }
}
```

---

## HTTP Patterns

```typescript
@Injectable({ providedIn: 'root' })
export class ProductService {
    private readonly http = inject(HttpClient);
    private readonly api  = inject(API_BASE_URL);

    // ✅ Simple GET — return Observable, let component manage subscription
    getAll(): Observable<Product[]> {
        return this.http.get<Product[]>(`${this.api}/products`);
    }

    // ✅ With error transformation
    getById(id: string): Observable<Product> {
        return this.http.get<Product>(`${this.api}/products/${id}`).pipe(
            catchError(err => {
                if (err.status === 404) throw new NotFoundException('Product', id);
                throw err;
            })
        );
    }

    // ✅ POST with typed response
    create(product: CreateProductDto): Observable<Product> {
        return this.http.post<Product>(`${this.api}/products`, product);
    }

    // ✅ shareReplay for shared data (avoid duplicate requests)
    private _categories$: Observable<Category[]> | null = null;
    getCategories(): Observable<Category[]> {
        return this._categories$ ??= this.http.get<Category[]>(`${this.api}/categories`).pipe(
            shareReplay(1)
        );
    }
}
```

---

## Common Operator Patterns

```typescript
// ✅ Search with debounce + cancellation
readonly searchResults$ = this.searchControl.valueChanges.pipe(
    startWith(''),
    debounceTime(300),
    distinctUntilChanged(),
    filter(term => term.length === 0 || term.length >= 2),
    switchMap(term => term
        ? this.service.search(term).pipe(catchError(() => of([])))
        : of([])
    )
);

// ✅ Load on route param change
readonly product$ = inject(ActivatedRoute).paramMap.pipe(
    map(params => params.get('id')!),
    distinctUntilChanged(),
    switchMap(id => this.productService.getById(id)),
    catchError(err => {
        this.router.navigate(['/not-found']);
        return EMPTY;
    })
);

// ✅ Poll every 30 seconds
readonly liveData$ = timer(0, 30_000).pipe(
    switchMap(() => this.dashboardService.getStats()),
    takeUntilDestroyed()
);

// ✅ Combine multiple streams
readonly viewModel$ = combineLatest({
    products:   this.productService.getAll(),
    categories: this.categoryService.getAll(),
    user:       this.authService.currentUser$
}).pipe(
    map(({ products, categories, user }) => ({
        products: products.filter(p => p.categoryId === user.preferredCategory),
        categories
    }))
);

// ✅ Retry with exponential backoff
readonly reliableData$ = this.service.getData().pipe(
    retry({
        count: 3,
        delay: (error, attempt) => timer(Math.pow(2, attempt) * 1000)
    }),
    catchError(() => of(null))
);
```

---

## BehaviorSubject vs Signal — When to Use Each

```typescript
// BehaviorSubject — use when:
// - You need RxJS operators on the state stream
// - Consumers need to subscribe reactively
// - You need combineLatest / withLatestFrom with other streams

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly _user$ = new BehaviorSubject<User | null>(null);
    readonly user$  = this._user$.asObservable();
    readonly isAuth$ = this._user$.pipe(map(u => u !== null));

    // Can combine with other streams
    readonly userOrders$ = this._user$.pipe(
        filter(Boolean),
        switchMap(u => this.orderService.getForUser(u.id))
    );
}

// Signal — use when:
// - Simple component local state
// - Template-bound reactive state (especially with computed)
// - You don't need RxJS operators

@Injectable({ providedIn: 'root' })
export class ThemeService {
    readonly theme    = signal<'light' | 'dark'>('light');
    readonly isDark   = computed(() => this.theme() === 'dark');
    readonly cssClass = computed(() => `theme-${this.theme()}`);

    toggle() { this.theme.update(t => t === 'light' ? 'dark' : 'light'); }
}
```
