import { Component, OnInit } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import { UploadBatchService } from '../upload-batch';

export interface CreateUploadBatchRequest {
  name: string;
}

@Component({
  selector: 'app-create-upload-batch',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './create-upload-batch.html',
  styleUrl: './create-upload-batch.scss',
})
export class CreateUploadBatch implements OnInit {
  form!: FormGroup;
  selectedFiles: File[] = [];
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
    });
  }

  onFilesSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const files = target.files;
    if (files) {
      this.selectedFiles = Array.from(files);
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }

    this.isSubmitting = true;
    const payload: CreateUploadBatchRequest = {
      name: this.form.get('name')?.value,
    };

    console.log('Enviando:', payload);
    console.log('Arquivos selecionados:', this.selectedFiles);

    this.uploadBatchService.create(payload).subscribe({
      next: (response) => {
        console.log('Lote criado com sucesso:', response);
        this.isSubmitting = false;
        this.form.reset();
        this.clearFiles();
      },
      error: (error) => {
        console.error('Erro ao criar lote:', error);
        this.isSubmitting = false;
      },
    });
  }

  clearFiles(): void {
    this.selectedFiles = [];
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }
}
