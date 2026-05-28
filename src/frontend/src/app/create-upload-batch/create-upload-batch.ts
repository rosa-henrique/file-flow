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

export interface BatchItem {
  file: File;
  title: string;
  tags: string | string[];
}

export interface CreateUploadBatchRequest {
  name: string;
  items: BatchItem[];
}

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
    private uploadBatchService: UploadBatchService
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
      this.items.at(index).patchValue({
        file: files[0],
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
      return;
    }

    this.isSubmitting = true;

    // Processa os itens, convertendo tags de string para array
    const processedItems: BatchItem[] = this.items.value.map((item: any) => ({
      file: item.file,
      title: item.title.trim(),
      tags: this.parseTags(item.tags),
    }));

    const payload: CreateUploadBatchRequest = {
      name: this.form.get('name')?.value.trim(),
      items: processedItems,
    };

    console.log('Enviando:', payload);

    this.uploadBatchService.create(payload).subscribe({
      next: (response) => {
        console.log('Lote criado com sucesso:', response);
        this.isSubmitting = false;
        this.form.reset();
        this.items.clear();
      },
      error: (error) => {
        console.error('Erro ao criar lote:', error);
        this.isSubmitting = false;
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
