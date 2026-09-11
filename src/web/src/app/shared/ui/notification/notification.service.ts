import { Injectable, signal } from '@angular/core';

export interface Notification {
  id: number;
  messageKey: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextId = 0;
  readonly notifications = signal<readonly Notification[]>([]);

  show(messageKey: string): void {
    const id = this.nextId++;
    this.notifications.update((list) => [...list, { id, messageKey }]);
    setTimeout(() => this.dismiss(id), 6000);
  }

  dismiss(id: number): void {
    this.notifications.update((list) => list.filter((n) => n.id !== id));
  }
}
