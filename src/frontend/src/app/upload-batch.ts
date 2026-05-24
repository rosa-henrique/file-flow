import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export enum UploadBatchStatus {
  PENDING = 'PENDING',
  PROCESSING = 'PROCESSING',
  COMPLETED = 'COMPLETED',
  PARTIAL = 'PARTIAL',
  FAILED = 'FAILED',
}

export const UploadBatchStatusLabel: Record<UploadBatchStatus, string> = {
  [UploadBatchStatus.PENDING]: 'Pendente',
  [UploadBatchStatus.PROCESSING]: 'Processando',
  [UploadBatchStatus.COMPLETED]: 'Completado',
  [UploadBatchStatus.PARTIAL]: 'Enviado Parcialmente',
  [UploadBatchStatus.FAILED]: 'Com Erro',
};

export interface GetUploadBatchesResponse {
  id: string; // Guid
  name: string;
  uploadBatchStatus: UploadBatchStatus;
  createdAt: Date;
  completedAt: Date | null;
  totalFile: number;
}

@Injectable({
  providedIn: 'root',
})
export class UploadBatchService {
  private apiUrl = 'api/upload-batches';

  constructor(private httpClient: HttpClient) {}

  getAll(): Observable<GetUploadBatchesResponse[]> {
    return this.httpClient.get<GetUploadBatchesResponse[]>(this.apiUrl);
  }

  create(payload: { name: string }): Observable<GetUploadBatchesResponse> {
    return this.httpClient.post<GetUploadBatchesResponse>(this.apiUrl, payload);
  }
}
