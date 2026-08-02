import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

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
  filesInfo: Array<{
    objectKey: string;
    originalFileName: string;
    mimeType: string;
    size: number;
    title: string;
    tags: string[];
    metadata: Record<string, unknown> | null;
  }>;
}

export interface CreateUploadBatchResponse {
  id: string;
}

export interface GetUploadBatchStatusResponse {
  id: string;
  status: UploadBatchStatus;
  timedOut: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class UploadBatchService {
  private readonly batchesApiUrl = 'api/upload-batches';
  private readonly createBatchApiUrl = 'api/upload-batch';

  constructor(private httpClient: HttpClient) {}

  getAll(): Observable<GetUploadBatchesResponse[]> {
    return this.httpClient.get<GetUploadBatchesResponse[]>(this.batchesApiUrl);
  }

  /**
   * Cria um lote de upload com arquivos já uploadados no storage
   * @param payload Contém os dados de criação do lote e referências dos arquivos já enviados
   */
  create(payload: CreateUploadBatchPayload): Observable<CreateUploadBatchResponse> {
    console.log(`[UploadBatchService] Criando lote: ${payload.name}`);

    return this.httpClient
      .post(this.createBatchApiUrl, payload, {
        observe: 'response',
      })
      .pipe(
        map((response) => ({ id: this.extractBatchId(response) }))
      );
  }

  getStatus(id: string): Observable<GetUploadBatchStatusResponse> {
    return this.httpClient.get<GetUploadBatchStatusResponse>(
      `${this.batchesApiUrl}/${id}/status`
    );
  }

  private extractBatchId(response: HttpResponse<unknown>): string {
    const bodyId = this.extractIdFromBody(response.body);
    if (bodyId) {
      return bodyId;
    }

    const location = response.headers.get('location');
    if (location) {
      const idFromLocation = location.split('/').pop();
      if (idFromLocation) {
        return idFromLocation;
      }
    }

    throw new Error('Nao foi possivel identificar o id do lote criado.');
  }

  private extractIdFromBody(body: unknown): string | null {
    if (typeof body === 'string' && body.trim().length > 0) {
      return body;
    }

    if (body && typeof body === 'object' && 'id' in body) {
      const id = (body as { id?: unknown }).id;
      if (typeof id === 'string' && id.trim().length > 0) {
        return id;
      }
    }

    return null;
  }
}
