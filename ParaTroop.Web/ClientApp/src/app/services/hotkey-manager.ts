import { DOCUMENT } from '@angular/common';
import { Inject, Injectable } from '@angular/core';
import { EventManager } from '@angular/platform-browser';
import { Observable } from 'rxjs';

export type HotkeyOptions = {
  element: any;
  keys: string;
  preventDefault: boolean;
}

@Injectable({ providedIn: 'root' })
export class HotkeyManager {
  defaults: Partial<HotkeyOptions> = {
    element: this.document
  }

  constructor(
    private eventManager: EventManager,
    @Inject(DOCUMENT) private document: Document) {
  }

  addShortcut(options: Partial<HotkeyOptions>) {
    const merged = { ...this.defaults, ...options };
    const event = `keydown.${merged.keys}`;

    return new Observable(observer => {
      const handler = (e) => {
        if (merged.preventDefault) {
          e.preventDefault();
        }

        observer.next(e);
      };

      const dispose = this.eventManager.addEventListener(
         merged.element, event, handler
      );

      return () => {
        dispose();
      };
    });
  }
}
