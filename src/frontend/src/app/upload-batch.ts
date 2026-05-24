import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export enum UploadBatchStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Completed = 'Completed',
  Failed = 'Failed',
}

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
}
