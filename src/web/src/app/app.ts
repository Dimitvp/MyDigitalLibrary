import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink, RouterOutlet } from '@angular/router';
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

  protected readonly auth = inject(AuthService);
  protected readonly languages: readonly SupportedLang[] = ['bg', 'en'];

  ngOnInit(): void {
    this.language.init();
  }

  protected get activeLang(): SupportedLang {
    return this.language.activeLang;
  }

  protected setLang(lang: SupportedLang): void {
    this.language.setLang(lang);
  }

  protected logout(): void {
    this.auth.logout().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }
}
