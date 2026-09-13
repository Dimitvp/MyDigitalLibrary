# Fix: logout button didn't redirect to login

**Date:** 2026-09-13
**Context:** Owner reported that clicking "logout" in the app header left
them sitting on the current page (library) instead of sending them to the
login screen.
**Status:** Complete.

## Root cause

`AuthService.logout()` (`src/web/src/app/core/auth/auth.service.ts`) posts
to `/api/v1/auth/logout` and sets the `currentUser` signal to `null` — it
never navigates anywhere. `App.logout()` (`src/web/src/app/app.ts`) just
subscribed to that call with no further action.

Navigation-based route protection (`authGuard`,
`src/web/src/app/core/auth/auth.guard.ts`) only runs when Angular's router
activates a route. Logging out doesn't trigger navigation by itself, so the
guard never got a chance to redirect — the user stayed on `/library` with
`isAuthenticated()` now false (which is why the logout button itself
disappeared from the header, the only visible effect).

## Fix

`App.logout()` now navigates to `/login` after the logout call completes:

```ts
protected logout(): void {
  this.auth
    .logout()
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => this.router.navigateByUrl('/login'));
}
```

`Router` injected alongside the existing services in `app.ts`.

## Verified

- `npx ng test --watch=false` in `src/web` — both existing `App` spec tests
  still pass (they use `provideRouter([])`, so the new `Router` injection
  resolves fine).

## Files changed

- `src/web/src/app/app.ts`

## Next

Nothing queued.
