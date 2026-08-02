import { Component, OnDestroy, OnInit } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  FormArray,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import {
  UploadBatchService,
  UploadBatchStatus,
  UploadBatchStatusLabel,
} from '../upload-batch';
import { UploadFile, GenerateUploadUrlResponse } from '../upload-file';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-create-upload-batch',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './create-upload-batch.html',
  styleUrl: './create-upload-batch.scss',
})
export class CreateUploadBatch implements OnInit {
  form!: FormGroup;
  isSubmitting = false;
  isStatusModalVisible = false;
  isPollingStatus = false;
  statusMessage: string | null = null;
  statusError: string | null = null;
  currentBatchId: string | null = null;
  currentBatchStatus: UploadBatchStatus = UploadBatchStatus.PROCESSING;

  private pollingTimerId: ReturnType<typeof setTimeout> | null = null;
  private completionRedirectTimerId: ReturnType<typeof setTimeout> | null = null;
  private statusSubscription: Subscription | null = null;
  private statusRetryAttempts = 0;

  private readonly pollingIntervalMs = 300;
  private readonly retryDelayMs = 1000;
  private readonly maxRetryAttempts = 3;
  private readonly completionRedirectDelayMs = 1200;

  protected readonly UploadBatchStatus = UploadBatchStatus;

  constructor(
    private fb: FormBuilder,
    private uploadBatchService: UploadBatchService,
    private uploadFile: UploadFile,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.initializeForm();
  }

  ngOnDestroy(): void {
    this.stopStatusPolling();
  }

  private initializeForm(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(3)]],
      items: this.fb.array([], [Validators.required, Validators.minLength(1)]),
    });
  }

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  addItem(): void {
    const itemGroup = this.fb.group({
      file: [null, Validators.required],
      title: ['', [Validators.required, Validators.minLength(2)]],
      tags: ['', Validators.required],
      // Dados do arquivo
      originalFileName: [null],
      mimeType: [null],
      size: [null],
      // Campos de upload
      uploadStatus: ['pending'], // 'pending' | 'uploading' | 'completed' | 'error'
      uploadProgress: [0],
      objectKey: [null],
      uploadError: [null],
      // Metadados técnicos (ex: resolução, duração, etc)
      metadata: [null],
    });
    this.items.push(itemGroup);
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  onFileSelected(event: Event, index: number): void {
    const target = event.target as HTMLInputElement;
    const files = target.files;

    if (files && files.length > 0) {
      const file = files[0];
      const itemControl = this.items.at(index);

      // Extrai metadados técnicos do arquivo
      this.extractFileMetadata(file).then((metadata) => {
        // Atualiza o arquivo e marca como uploading
        itemControl.patchValue({
          file,
          originalFileName: file.name,
          mimeType: file.type,
          size: file.size,
          metadata,
          uploadStatus: 'uploading',
          uploadProgress: 0,
          uploadError: null,
        });

        console.log(`[CreateUploadBatch] Iniciando upload do arquivo: ${file.name}`);

        // Faz upload imediatamente ao selecionar
        this.uploadFile.uploadFile({ file }).subscribe({
          next: (response: GenerateUploadUrlResponse) => {
            console.log(
              `[CreateUploadBatch] Upload concluído: ${file.name}`,
              response
            );
            itemControl.patchValue({
              uploadStatus: 'completed',
              uploadProgress: 100,
              objectKey: response.objectKey,
            });
          },
          error: (error) => {
            console.error(
              `[CreateUploadBatch] Erro ao fazer upload: ${file.name}`,
              error
            );
            itemControl.patchValue({
              uploadStatus: 'error',
              uploadError: error.message || 'Erro ao fazer upload',
            });
          },
        });
      });
    }
  }

  private async extractFileMetadata(file: File): Promise<any> {
    const metadata: any = {};

    // Extrai metadados para imagens
    if (file.type.startsWith('image/')) {
      try {
        const imageMetadata = await this.extractImageMetadata(file);
        Object.assign(metadata, imageMetadata);
      } catch (error) {
        console.warn('[CreateUploadBatch] Erro ao extrair metadados de imagem:', error);
      }
    }

    // Extrai metadados para vídeos
    if (file.type.startsWith('video/')) {
      try {
        const videoMetadata = await this.extractVideoMetadata(file);
        Object.assign(metadata, videoMetadata);
      } catch (error) {
        console.warn('[CreateUploadBatch] Erro ao extrair metadados de vídeo:', error);
      }
    }

    return Object.keys(metadata).length > 0 ? metadata : null;
  }

  private extractImageMetadata(file: File): Promise<any> {
    return new Promise((resolve) => {
      const reader = new FileReader();

      reader.onload = (e: any) => {
        const img = new Image();
        img.onload = () => {
          resolve({
            type: 'image',
            width: img.width,
            height: img.height,
            resolution: `${img.width}x${img.height}`,
          });
        };
        img.onerror = () => {
          resolve({});
        };
        img.src = e.target.result;
      };

      reader.onerror = () => {
        resolve({});
      };

      reader.readAsDataURL(file);
    });
  }

  private extractVideoMetadata(file: File): Promise<any> {
    return new Promise((resolve) => {
      const reader = new FileReader();

      reader.onload = (e: any) => {
        const video = document.createElement('video');
        video.onloadedmetadata = () => {
          resolve({
            type: 'video',
            duration: Math.round(video.duration),
            durationFormatted: this.formatDuration(video.duration),
          });
        };
        video.onerror = () => {
          resolve({});
        };
        video.src = e.target.result;
      };

      reader.onerror = () => {
        resolve({});
      };

      reader.readAsDataURL(file);
    });
  }

  private formatDuration(seconds: number): string {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);

    if (hours > 0) {
      return `${hours}h ${minutes}m ${secs}s`;
    }
    if (minutes > 0) {
      return `${minutes}m ${secs}s`;
    }
    return `${secs}s`;
  }

  getFileDisplayName(index: number): string {
    const file = this.items.at(index).get('file')?.value;
    return file?.name || 'Nenhum arquivo selecionado';
  }

  getFileSize(index: number): string {
    const file = this.items.at(index).get('file')?.value;
    if (!file) return '';
    return this.formatFileSize(file.size);
  }

  onSubmit(): void {
    if (this.form.invalid || this.items.length === 0) {
      console.warn('[CreateUploadBatch] Formulário inválido ou sem itens');
      return;
    }

    // Valida se todos os uploads foram completados com sucesso
    const itemsArray = this.items.value;
    const allUploadsCompleted = itemsArray.every(
      (item: any) => item.uploadStatus === 'completed' && item.objectKey
    );

    if (!allUploadsCompleted) {
      console.warn('[CreateUploadBatch] Nem todos os arquivos foram uploadados com sucesso');
      alert('Por favor, certifique-se de que todos os arquivos foram uploadados com sucesso.');
      return;
    }

    this.isSubmitting = true;

    // Constrói o payload com os dados dos itens
    const payload = {
      name: this.form.get('name')?.value.trim(),
      filesInfo: itemsArray.map((item: any) => ({
        objectKey: item.objectKey, // Object key do arquivo no storage
        originalFileName: item.originalFileName,
        mimeType: item.mimeType,
        size: item.size,
        title: item.title.trim(),
        tags: this.parseTags(item.tags),
        metadata: item.metadata || null, // Dados técnicos extraídos automaticamente
      })),
    };

    console.log('[CreateUploadBatch] Enviando lote com arquivos e metadados:', payload);

    this.uploadBatchService.create(payload).subscribe({
      next: (response) => {
        console.log('[CreateUploadBatch] Lote criado com sucesso:', response);
        this.isSubmitting = false;

        this.form.reset();
        this.items.clear();

        this.openStatusModal(response.id);
        this.startStatusLongPolling(response.id);
      },
      error: (error) => {
        console.error('[CreateUploadBatch] Erro ao criar lote:', error);
        this.isSubmitting = false;
        alert('Erro ao criar o lote. Tente novamente.');
      },
    });
  }

  private parseTags(tagsInput: string): string[] {
    if (!tagsInput || typeof tagsInput !== 'string') {
      return [];
    }
    return tagsInput
      .split(',')
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0);
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }

  onModalClose(): void {
    this.stopStatusPolling();
    this.closeModal();
    this.goToBatchResultScreen();
  }

  onBackToList(): void {
    this.closeModal();
    this.goToBatchResultScreen();
  }

  onViewDetails(): void {
    if (!this.currentBatchId) {
      return;
    }

    this.stopStatusPolling();
    this.closeModal();
    this.router.navigate(['/upload-batches', this.currentBatchId]);
  }

  isFinalStatus(): boolean {
    return (
      this.currentBatchStatus === UploadBatchStatus.COMPLETED ||
      this.currentBatchStatus === UploadBatchStatus.PARTIAL ||
      this.currentBatchStatus === UploadBatchStatus.FAILED
    );
  }

  getFinalStatusTitle(): string {
    if (this.currentBatchStatus === UploadBatchStatus.COMPLETED) {
      return 'Lote concluido com sucesso';
    }

    if (this.currentBatchStatus === UploadBatchStatus.PARTIAL) {
      return 'Lote concluido parcialmente';
    }

    if (this.currentBatchStatus === UploadBatchStatus.FAILED) {
      return 'Falha no processamento do lote';
    }

    return 'Processamento em andamento';
  }

  getFinalStatusDescription(): string {
    if (this.currentBatchStatus === UploadBatchStatus.COMPLETED) {
      return 'Todos os arquivos foram processados com sucesso.';
    }

    if (this.currentBatchStatus === UploadBatchStatus.PARTIAL) {
      return 'Uma parte dos arquivos foi processada. Verifique os detalhes para identificar os itens com falha.';
    }

    if (this.currentBatchStatus === UploadBatchStatus.FAILED) {
      return 'Nao foi possivel processar este lote. Voce pode acessar os detalhes para investigar o motivo.';
    }

    return 'Aguarde enquanto finalizamos o processamento do lote.';
  }

  getHumanizedStatus(status: UploadBatchStatus): string {
    return UploadBatchStatusLabel[status] ?? status;
  }

  private openStatusModal(batchId: string): void {
    this.currentBatchId = batchId;
    this.currentBatchStatus = UploadBatchStatus.PROCESSING;
    this.statusError = null;
    this.statusMessage = 'Estamos processando os arquivos enviados. Isso pode levar alguns instantes.';
    this.isPollingStatus = true;
    this.isStatusModalVisible = true;
  }

  private closeModal(): void {
    this.isStatusModalVisible = false;
    this.statusMessage = null;
    this.statusError = null;
    this.currentBatchId = null;
  }

  private startStatusLongPolling(batchId: string): void {
    this.stopStatusPolling();
    this.statusRetryAttempts = 0;
    this.requestBatchStatus(batchId);
  }

  private requestBatchStatus(batchId: string): void {
    this.statusSubscription = this.uploadBatchService.getStatus(batchId).subscribe({
      next: (statusResponse) => {
        this.statusSubscription = null;
        this.statusRetryAttempts = 0;
        this.currentBatchStatus = statusResponse.status;

        if (this.isFinalStatusValue(statusResponse.status)) {
          this.statusMessage = 'Processamento finalizado. Redirecionando para a listagem...';
          this.statusError = null;
          this.isPollingStatus = false;
          this.scheduleCompletionRedirect();
          return;
        }

        if (statusResponse.status === UploadBatchStatus.PROCESSING) {
          this.statusMessage = 'Lote em processamento. Vamos atualizar automaticamente quando houver mudanca de status.';
          this.scheduleNextStatusRequest(batchId);
          return;
        }

        this.isPollingStatus = false;
        this.statusError = null;
        this.statusMessage = null;
      },
      error: (error) => {
        this.statusSubscription = null;
        console.error('[CreateUploadBatch] Erro ao consultar status do lote:', error);

        this.statusRetryAttempts += 1;
        if (this.statusRetryAttempts <= this.maxRetryAttempts) {
          this.isPollingStatus = true;
          this.statusError = `Falha ao consultar status. Tentando novamente (${this.statusRetryAttempts}/${this.maxRetryAttempts})...`;
          this.scheduleNextStatusRequest(batchId, this.retryDelayMs);
          return;
        }

        this.isPollingStatus = false;
        this.statusError = 'Nao foi possivel atualizar o status do lote apos 3 tentativas. Tente novamente nos detalhes.';
      },
    });
  }

  private isFinalStatusValue(status: UploadBatchStatus): boolean {
    return (
      status === UploadBatchStatus.COMPLETED ||
      status === UploadBatchStatus.PARTIAL ||
      status === UploadBatchStatus.FAILED
    );
  }

  private scheduleNextStatusRequest(batchId: string, delayMs = this.pollingIntervalMs): void {
    this.stopScheduledPolling();
    this.pollingTimerId = setTimeout(() => {
      this.requestBatchStatus(batchId);
    }, delayMs);
  }

  private stopScheduledPolling(): void {
    if (this.pollingTimerId) {
      clearTimeout(this.pollingTimerId);
      this.pollingTimerId = null;
    }
  }

  private scheduleCompletionRedirect(): void {
    this.clearCompletionRedirect();
    this.completionRedirectTimerId = setTimeout(() => {
      this.closeModal();
      this.goToBatchResultScreen();
    }, this.completionRedirectDelayMs);
  }

  private clearCompletionRedirect(): void {
    if (this.completionRedirectTimerId) {
      clearTimeout(this.completionRedirectTimerId);
      this.completionRedirectTimerId = null;
    }
  }

  private stopStatusPolling(): void {
    this.stopScheduledPolling();
    this.clearCompletionRedirect();
    this.statusSubscription?.unsubscribe();
    this.statusSubscription = null;
    this.isPollingStatus = false;
  }

  private goToBatchResultScreen(): void {
    // TODO: atualizar para a rota da tela de resultado quando ela for criada.
    this.router.navigate(['/']);
  }
}
