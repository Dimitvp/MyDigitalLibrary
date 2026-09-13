import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { NotificationService } from './notification.service';

@Component({
  selector: 'app-notification',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="notification-list" role="status" aria-live="polite">
      @for (notification of notifications.notifications(); track notification.id) {
        <div class="notification">
          <span>{{ notification.messageKey | transloco: notification.params }}</span>
          <button type="button" (click)="notifications.dismiss(notification.id)" aria-label="dismiss">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    .notification-list {
      position: fixed;
      top: 1rem;
      right: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      z-index: 1000;
      max-width: min(90vw, 24rem);
    }

    .notification {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      background: #3a1a1a;
      color: #ffd7d7;
      border: 1px solid #5a2a2a;
      border-radius: 0.5rem;
      padding: 0.75rem 1rem;
      font-size: 0.9rem;
    }

    button {
      background: none;
      border: none;
      color: inherit;
      cursor: pointer;
      font-size: 1.1rem;
      line-height: 1;
    }
  `,
})
export class NotificationComponent {
  protected readonly notifications = inject(NotificationService);
}
