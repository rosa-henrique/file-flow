import { Component, OnInit } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  FormArray,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import { UploadBatchService } from '../upload-batch';
import { UploadFile, GenerateUploadUrlResponse } from '../upload-file';

@Component({
  selector: 'app-create-upload-batch',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './create-upload-batch.html',
  styleUrl: './create-upload-batch.scss',
})
export class CreateUploadBatch implements OnInit {
  form!: FormGroup;
  isSubmitting = false;

  constructor(
    private fb: FormBuilder,
    private uploadBatchService: UploadBatchService,
    private uploadFile: UploadFile
  ) {}

  ngOnInit(): void {
    this.initializeForm();
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
      // Campos de upload
      uploadStatus: ['pending'], // 'pending' | 'uploading' | 'completed' | 'error'
      uploadProgress: [0],
      uploadUrl: [null],
      uploadError: [null],
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

      // Atualiza o arquivo e marca como uploading
      itemControl.patchValue({
        file,
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
            uploadUrl: (response as any).uploadUrl,
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
    }
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
      (item: any) => item.uploadStatus === 'completed' && item.uploadUrl
    );

    if (!allUploadsCompleted) {
      console.warn('[CreateUploadBatch] Nem todos os arquivos foram uploadados com sucesso');
      alert('Por favor, certifique-se de que todos os arquivos foram uploadados com sucesso.');
      return;
    }

    this.isSubmitting = true;

    // Constrói o payload com os dados dos itens e URLs já uploadadas
    const payload = {
      name: this.form.get('name')?.value.trim(),
      items: itemsArray.map((item: any) => ({
        title: item.title.trim(),
        tags: this.parseTags(item.tags),
        uploadUrl: item.uploadUrl, // URL já no storage
      })),
    };

    console.log('[CreateUploadBatch] Enviando lote com arquivos já uploadados:', payload);

    this.uploadBatchService.create(payload).subscribe({
      next: (response) => {
        console.log('[CreateUploadBatch] Lote criado com sucesso:', response);
        this.isSubmitting = false;
        this.form.reset();
        this.items.clear();
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
}
