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

export interface CreateUploadBatchPayload {
  name: string;
  items: Array<{
    title: string;
    tags: string[];
    uploadUrl: string; // URL já no object storage (S3, etc)
  }>;
}

@Injectable({
  providedIn: 'root',
})
export class UploadBatchService {
  private apiUrl = 'api/upload-batch';

  constructor(private httpClient: HttpClient) {}

  getAll(): Observable<GetUploadBatchesResponse[]> {
    return this.httpClient.get<GetUploadBatchesResponse[]>(this.apiUrl);
  }

  /**
   * Cria um lote de upload com arquivos já uploadados no storage
   * @param payload Contém name do lote e items com metadata + uploadUrl (já no storage)
   */
  create(payload: CreateUploadBatchPayload): Observable<void> {
    console.log(`[UploadBatchService] Criando lote: ${payload.name}`);
    return this.httpClient.post<void>(this.apiUrl, payload);
  }
}
