import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from './core/auth/auth.service';
import { LanguageService, type SupportedLang } from './core/i18n/language.service';
import { NotificationComponent } from './shared/ui/notification/notification';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterOutlet, TranslocoPipe, NotificationComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly language = inject(LanguageService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthService);
  protected readonly languages: readonly SupportedLang[] = ['bg', 'en'];

  // Narrow-viewport nav — collapsed by default, toggled by the hamburger
  // button, closed again on any link tap (see closeMenu()) so it never
  // lingers open across a route change (plan: phone-width header was
  // overflowing off-screen with no wrap/collapse at all).
  protected readonly menuOpen = signal(false);

  ngOnInit(): void {
    this.language.init();
  }

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected get activeLang(): SupportedLang {
    return this.language.activeLang;
  }

  protected setLang(lang: SupportedLang): void {
    this.language.setLang(lang);
  }

  protected logout(): void {
    this.auth
      .logout()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.router.navigateByUrl('/login'));
  }
}
