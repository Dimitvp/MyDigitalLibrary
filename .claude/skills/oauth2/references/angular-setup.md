# Angular OAuth 2.0 Setup — Reference

## Auth Service with Authorization Code + PKCE

```typescript
// auth.service.ts
@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly config: OAuthConfig = {
        issuer:        environment.authUrl,
        clientId:      environment.clientId,
        redirectUri:   `${window.location.origin}/callback`,
        scope:         'openid profile email products.read offline_access',
        responseType:  'code',
        useSilentRefresh: false,
    };

    // Use a library — do not implement PKCE + token exchange manually
    // Recommended: angular-oauth2-oidc or auth0-angular
    constructor(private oauthService: OAuthService) {
        this.oauthService.configure(this.config);
        this.oauthService.setupAutomaticSilentRefresh();
    }

    async init(): Promise<void> {
        await this.oauthService.loadDiscoveryDocumentAndTryLogin();
    }

    login():  void { this.oauthService.initCodeFlow(); }
    logout(): void { this.oauthService.logOut(); }

    get isAuthenticated(): boolean { return this.oauthService.hasValidAccessToken(); }
    get accessToken(): string      { return this.oauthService.getAccessToken(); }
    get idToken(): string          { return this.oauthService.getIdToken(); }

    // Claims from ID token — NOT from access token
    get userClaims(): Record<string, unknown> {
        return this.oauthService.getIdentityClaims() as Record<string, unknown>;
    }
}
```

---

## HTTP Interceptor — Bearer Token Attachment

```typescript
// auth.interceptor.ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const auth   = inject(AuthService);
    const apiUrl = inject(API_BASE_URL);

    // ✅ Only attach token to YOUR API — never to third-party URLs
    const isOwnApi = req.url.startsWith(apiUrl);
    if (!isOwnApi || !auth.isAuthenticated) return next(req);

    return next(req.clone({
        setHeaders: { Authorization: `Bearer ${auth.accessToken}` }
    }));
};

// 401 Interceptor — attempt token refresh, then retry
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
    const auth    = inject(AuthService);
    const refresh  = inject(TokenRefreshService);

    return next(req).pipe(
        catchError((err: HttpErrorResponse) => {
            if (err.status === 401 && !req.url.includes('/connect/token')) {
                return refresh.refreshToken().pipe(
                    switchMap(newToken => next(req.clone({
                        setHeaders: { Authorization: `Bearer ${newToken}` }
                    })))
                );
            }
            return throwError(() => err);
        })
    );
};

// Registration order matters
provideHttpClient(withInterceptors([
    correlationIdInterceptor,  // 1st — add correlation ID
    authInterceptor,           // 2nd — add Bearer token
    refreshInterceptor,        // 3rd — handle 401 → refresh
    errorInterceptor,          // 4th — handle other errors
]))
```

---

## Callback Component

```typescript
// callback.component.ts
@Component({
    standalone: true,
    template: `<app-spinner/>`,
})
export class CallbackComponent implements OnInit {
    private readonly authService = inject(AuthService);
    private readonly router      = inject(Router);

    async ngOnInit(): Promise<void> {
        // OAuthService handles code exchange automatically after loadDiscoveryDocumentAndTryLogin()
        // Redirect to saved return URL or home
        const returnUrl = sessionStorage.getItem('returnUrl') || '/';
        sessionStorage.removeItem('returnUrl');
        await this.router.navigateByUrl(returnUrl);
    }
}
```

---

## Auth Guard

```typescript
// auth.guard.ts
export const authGuard: CanActivateFn = (route, state) => {
    const auth   = inject(AuthService);
    const router = inject(Router);

    if (auth.isAuthenticated) return true;

    // Save intended URL for post-login redirect
    sessionStorage.setItem('returnUrl', state.url);
    auth.login();
    return false;
};

// Routes
export const APP_ROUTES: Routes = [
    { path: 'callback', loadComponent: () => import('./callback.component').then(m => m.CallbackComponent) },
    { path: 'login',    loadComponent: () => import('./login.component').then(m => m.LoginComponent) },
    {
        path: '',
        canActivate: [authGuard],
        children: [
            { path: 'products', loadChildren: () => import('./features/products/products.routes').then(m => m.PRODUCTS_ROUTES) },
            { path: 'orders',   loadChildren: () => import('./features/orders/orders.routes').then(m => m.ORDERS_ROUTES) },
        ]
    }
];
```

---

## Using angular-oauth2-oidc Library

```typescript
// Installation: npm install angular-oauth2-oidc

// app.config.ts
import { provideOAuthClient } from 'angular-oauth2-oidc';

export const appConfig: ApplicationConfig = {
    providers: [
        provideRouter(APP_ROUTES),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideOAuthClient({
            resourceServer: {
                allowedUrls:    [environment.apiBaseUrl],
                sendAccessToken: true  // automatically attaches token to allowed URLs
            }
        })
    ]
};

// OAuthConfig
export const oauthConfig: AuthConfig = {
    issuer:                environment.authUrl,
    clientId:              'angular-spa',
    redirectUri:           window.location.origin + '/callback',
    silentRefreshRedirectUri: window.location.origin + '/silent-refresh.html',
    scope:                 'openid profile email products.read offline_access',
    responseType:          'code',
    oidc:                  true,
    useSilentRefresh:      true,
    sessionChecksEnabled:  true,
    showDebugInformation:  !environment.production,
    clearHashAfterLogin:   false,
    nonceStateSeparator:   'semicolon',
};
```
