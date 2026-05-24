import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { GetUploadBatchesResponse, UploadBatchService, UploadBatchStatusLabel } from '../upload-batch';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule, MatTableModule, MatCardModule, MatButtonModule],
  templateUrl: './home.html',
  styleUrls: ['./home.scss'],
})
export class Home implements OnInit {
  protected readonly displayedColumns = ['name', 'uploadBatchStatus', 'totalFile', 'createdAt', 'completedAt'];
  protected batches: GetUploadBatchesResponse[] = [];
  protected isLoading = signal(true);
  protected error: string | null = null;

  constructor(private uploadBatchService: UploadBatchService) {}

  ngOnInit(): void {
    this.loadBatches();
  }

  private loadBatches(): void {
    this.isLoading.set(true);
    this.error = null;
    this.uploadBatchService.getAll().subscribe({
      next: (batches) => {
        console.log('NEXT executou', batches);

        this.batches = batches;
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Erro ao carregar lotes:', error);
        this.error = 'Erro ao carregar os lotes. Tente novamente.';
        this.isLoading.set(false);
      },
    });
  }

  formatDate(date: Date | null): string {
    if (!date) return '-';
    return new Date(date).toLocaleDateString('pt-BR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });
  }

  getStatusClass(status: string): string {
    return 'status-' + status.toLowerCase();
  }

  getStatusLabel(status: string): string {
    return UploadBatchStatusLabel[status as keyof typeof UploadBatchStatusLabel] || status;
  }
}
