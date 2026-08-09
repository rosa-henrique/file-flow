import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LoadingOverlay } from '../shared/loading-overlay/loading-overlay';
import {
  GetUploadBatchByIdMediaAssetResponse,
  GetUploadBatchByIdResponse,
  UploadBatchService,
  UploadBatchStatus,
  UploadBatchStatusLabel,
} from '../upload-batch';

@Component({
  selector: 'app-upload-batch-details',
  imports: [CommonModule, RouterModule, LoadingOverlay],
  templateUrl: './upload-batch-details.html',
  styleUrl: './upload-batch-details.scss',
})
export class UploadBatchDetails implements OnInit {
  protected readonly statusLabels = UploadBatchStatusLabel;
  protected readonly mediaAssetStatusLabels: Record<string, string> = {
    PENDING: 'Pendente',
    MIGRATING: 'Migrando',
    MIGRATED: 'Migrado',
    FAILED: 'Falha',
    DELETION_PENDING: 'Exclusao Pendente',
    DELETED: 'Excluido',
  };

  protected readonly isLoading = signal(false);
  protected readonly isReprocessing = signal(false);
  protected readonly reprocessFeedback = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly batch = signal<GetUploadBatchByIdResponse | null>(null);

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly uploadBatchService = inject(UploadBatchService);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');

      if (!id) {
        this.batch.set(null);
        return;
      }

      this.loadBatch(id);
    });
  }

  protected formatDate(date: Date | null): string {
    if (!date) {
      return '-';
    }

    return new Date(date).toLocaleString('pt-BR');
  }

  protected formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';

    const base = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const unitIndex = Math.floor(Math.log(bytes) / Math.log(base));
    const value = bytes / Math.pow(base, unitIndex);

    return `${Math.round(value * 100) / 100} ${sizes[unitIndex]}`;
  }

  protected formatStatus(status: UploadBatchStatus): string {
    return this.statusLabels[status] ?? status;
  }

  protected getSortedMediaAssets(): GetUploadBatchByIdMediaAssetResponse[] {
    const currentBatch = this.batch();
    if (!currentBatch) {
      return [];
    }

    return [...currentBatch.mediaAssets].sort((a, b) => {
      const bTime = new Date(b.createdAt).getTime();
      const aTime = new Date(a.createdAt).getTime();
      return bTime - aTime;
    });
  }

  protected getMediaAssetStatusLabel(status: string): string {
    return this.mediaAssetStatusLabels[status] ?? status;
  }

  protected getMediaAssetStatusClass(status: string): string {
    return `status-badge status-badge--${status.toLowerCase().replace(/_/g, '-')}`;
  }

  protected shouldShowReprocessButton(status: UploadBatchStatus): boolean {
    return status === UploadBatchStatus.FAILED || status === UploadBatchStatus.PARTIAL;
  }

  protected onReprocessFailedFiles(batchId: string): void {
    this.isReprocessing.set(true);
    this.reprocessFeedback.set(null);
    this.uploadBatchService.reprocess(batchId).subscribe({
      next: () => {
        this.isReprocessing.set(false);
        this.reprocessFeedback.set('Reprocessamento solicitado com sucesso. Feche para atualizar o conteudo.');
      },
      error: () => {
        this.isReprocessing.set(false);
        this.error.set('Nao foi possivel solicitar o reprocessamento. Tente novamente.');
      },
    });
  }

  protected closeReprocessFeedback(): void {
    this.reprocessFeedback.set(null);

    const currentBatch = this.batch();
    if (!currentBatch) {
      return;
    }

    this.loadBatch(currentBatch.id);
  }

  protected getAssetTitle(asset: GetUploadBatchByIdMediaAssetResponse): string {
    return asset.title?.trim() || asset.originalFileName;
  }

  protected getMetadataString(metadata: Record<string, unknown> | null): string {
    if (!metadata) {
      return 'Sem metadados tecnicos.';
    }

    return JSON.stringify(metadata, null, 2);
  }

  protected hasMetadata(metadata: Record<string, unknown> | null): boolean {
    return !!metadata && Object.keys(metadata).length > 0;
  }

  private loadBatch(id: string): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.uploadBatchService.getById(id).subscribe({
      next: (batch) => {
        this.batch.set(batch);
        this.isLoading.set(false);
      },
      error: () => {
        this.batch.set(null);
        this.error.set('Nao foi possivel carregar os detalhes do lote. Confira o ID e tente novamente.');
        this.isLoading.set(false);
      },
    });
  }

}
