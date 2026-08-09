import { Component, input } from '@angular/core';

@Component({
  selector: 'app-loading-overlay',
  template: `
    <div class="loading-overlay" role="status" aria-live="polite">
      <div class="loading-overlay__card">
        <span class="loading-overlay__spinner" aria-hidden="true"></span>
        <p class="loading-overlay__message">{{ message() }}</p>
      </div>
    </div>
  `,
  styles: [
    `
      .loading-overlay {
        position: fixed;
        inset: 0;
        background: rgba(20, 19, 19, 0.72);
        display: grid;
        place-items: center;
        z-index: 1500;
      }

      .loading-overlay__card {
        display: grid;
        gap: 0.8rem;
        justify-items: center;
        background: #201f20;
        border: 1px solid #4f378a;
        border-radius: 1rem;
        padding: 1rem 1.2rem;
        min-width: 260px;
      }

      .loading-overlay__spinner {
        width: 28px;
        height: 28px;
        border-radius: 50%;
        border: 3px solid rgba(207, 188, 255, 0.25);
        border-top-color: #cfbcff;
        animation: spin 0.85s linear infinite;
      }

      .loading-overlay__message {
        margin: 0;
        color: #e6e1e1;
        font-size: 0.92rem;
      }

      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class LoadingOverlay {
  message = input('Carregando...');
}
