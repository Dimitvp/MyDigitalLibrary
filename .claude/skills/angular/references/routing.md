# Routing — Reference

## Modern App Routes Setup

```typescript
// app.routes.ts
export const APP_ROUTES: Routes = [
    { path: '', redirectTo: 'home', pathMatch: 'full' },

    // Public routes
    {
        path: 'home',
        loadComponent: () => import('./features/home/home.component')
            .then(m => m.HomeComponent)
    },

    // Auth routes
    {
        path: 'auth',
        loadChildren: () => import('./features/auth/auth.routes')
            .then(m => m.AUTH_ROUTES)
    },

    // Protected feature routes
    {
        path: 'products',
        canActivate: [authGuard],
        loadChildren: () => import('./features/products/products.routes')
            .then(m => m.PRODUCTS_ROUTES)
    },

    {
        path: 'admin',
        canActivate: [authGuard, permissionGuard('admin.access')],
        loadChildren: () => import('./features/admin/admin.routes')
            .then(m => m.ADMIN_ROUTES)
    },

    { path: '**', loadComponent: () => import('./features/not-found/not-found.component')
        .then(m => m.NotFoundComponent) }
];

// main.ts
bootstrapApplication(AppComponent, {
    providers: [
        provideRouter(APP_ROUTES, withComponentInputBinding(), withViewTransitions()),
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, correlationIdInterceptor])),
        provideAnimations(),
        { provide: API_BASE_URL, useValue: environment.apiBaseUrl }
    ]
});
```

---

## Functional Guards (Angular 15+)

```typescript
// guards/auth.guard.ts
export const authGuard: CanActivateFn = (route, state) => {
    const auth   = inject(AuthService);
    const router = inject(Router);

    if (auth.isAuthenticated()) return true;

    // Preserve the attempted URL for redirect after login
    return router.createUrlTree(['/auth/login'], {
        queryParams: { returnUrl: state.url }
    });
};

// Parameterised guard factory
export const permissionGuard = (permission: string): CanActivateFn => () => {
    const auth   = inject(AuthService);
    const router = inject(Router);

    return auth.hasPermission(permission)
        ? true
        : router.createUrlTree(['/forbidden']);
};

// Child route guard
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
    component.hasUnsavedChanges()
        ? inject(ConfirmDialogService).confirm('Discard unsaved changes?')
        : true;

export interface HasUnsavedChanges {
    hasUnsavedChanges(): boolean;
}
```

---

## Functional Resolvers (Angular 15+)

```typescript
// resolvers/product.resolver.ts
export const productResolver: ResolveFn<Product> = (route) => {
    const productService = inject(ProductService);
    const router         = inject(Router);

    return productService.getById(route.paramMap.get('id')!).pipe(
        catchError(() => {
            router.navigate(['/not-found']);
            return EMPTY;
        })
    );
};

// Route definition
{
    path:     ':id',
    component: ProductDetailComponent,
    resolve:  { product: productResolver },
    // Access in component: inject(ActivatedRoute).snapshot.data['product']
    // Or with withComponentInputBinding(): @Input() product!: Product;
}
```

---

## Route Constants — Avoid Magic Strings

```typescript
// app.routes.const.ts
export const AppRoutes = {
    Home:     'home',
    Auth: {
        Login:    'auth/login',
        Register: 'auth/register'
    },
    Products: {
        List:   'products',
        Detail: (id: string) => `products/${id}`,
        Create: 'products/create',
        Edit:   (id: string) => `products/${id}/edit`
    },
    Admin: {
        Dashboard: 'admin',
        Users:     'admin/users'
    }
} as const;

// Usage — no magic strings
this.router.navigate([AppRoutes.Products.Detail(productId)]);
// Template
// <a [routerLink]="[AppRoutes.Auth.Login]">Login</a>
```

---

## withComponentInputBinding — Route Params as @Input

```typescript
// Enable in main.ts
provideRouter(routes, withComponentInputBinding())

// Component receives route params, query params, and resolve data as @Input
@Component({ standalone: true })
export class ProductDetailComponent {
    @Input() id!: string;           // from route param :id
    @Input() category?: string;     // from query param ?category=
    @Input() product!: Product;     // from resolve: { product: productResolver }

    // No need to inject ActivatedRoute for simple cases
}
```

---

## Preloading Strategy

```typescript
// Preload all lazy routes after initial load (good for small apps)
provideRouter(routes, withPreloading(PreloadAllModules))

// Custom preloading — only preload routes marked with data.preload = true
export class SelectivePreloadingStrategy implements PreloadingStrategy {
    preload(route: Route, load: () => Observable<unknown>): Observable<unknown> {
        return route.data?.['preload'] ? load() : EMPTY;
    }
}

provideRouter(routes, withPreloading(SelectivePreloadingStrategy))

// Route definition
{
    path: 'products',
    data: { preload: true },
    loadChildren: () => import('./features/products/products.routes').then(m => m.PRODUCTS_ROUTES)
}
```
