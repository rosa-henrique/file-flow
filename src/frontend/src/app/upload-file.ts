import { HttpClient, HttpRequest, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, Observable, throwError, switchMap, of, finalize } from 'rxjs';

export interface GenerateUploadUrlRequest {
  fileSize: number;
  contentType: string;
}

export interface GenerateUploadUrlResponse {
  type: "SIMPLE"|"MULTIPART";
}

export interface GenerateUploadUrlSimpleResponse extends GenerateUploadUrlResponse {
  uploadUrl: string;
}

export interface GenerateUploadUrlMultiPartResponse extends GenerateUploadUrlResponse {

}

@Injectable({
  providedIn: 'root',
})
export class UploadFile {
  private readonly apiUrl = 'api/file';

  constructor(private httpClient: HttpClient) {}

  uploadFile(payload: { file: File }): Observable<GenerateUploadUrlResponse> {
    const request: GenerateUploadUrlRequest = {
      fileSize: payload.file.size,
      contentType: payload.file.type || 'application/octet-stream',
    };

    return this.httpClient
      .post<GenerateUploadUrlResponse>(`${this.apiUrl}/generate-upload-url`, request)
      .pipe(
        switchMap((response) => this.handleGenerateUploadUrlResponse(response, payload.file)),
        catchError((error) => {
          console.error('[UploadFile] Erro ao gerar URL de upload:', error);
          return throwError(
            () =>
              new Error(
                error?.error?.message ||
                  'Falha ao processar o arquivo. Tente novamente.'
              )
          );
        })
      );
  }

  private handleGenerateUploadUrlResponse(
    response: GenerateUploadUrlResponse,
    file: File
  ): Observable<GenerateUploadUrlResponse> {
    if (response.type === 'SIMPLE') {
      return this.handleSimpleUpload(response as GenerateUploadUrlSimpleResponse, file);
    } else  {
      return this.handleMultiPartUpload(response as GenerateUploadUrlMultiPartResponse, file);
    }
  }

  private handleSimpleUpload(
    response: GenerateUploadUrlSimpleResponse,
    file: File
  ): Observable<GenerateUploadUrlResponse> {
    console.log('[UploadFile] URL de upload simples gerada com sucesso:', response);

    // Cria um HttpRequest com headers customizados para upload direto (S3, etc)
    const headers = new HttpHeaders({
      'Content-Type': file.type || 'application/octet-stream',
    });

    const request = new HttpRequest('PUT', response.uploadUrl, file, {
      headers,
      reportProgress: true,
    });

    return this.httpClient
      .request<void>(request)
      .pipe(
        catchError((error) => {
          console.error('[UploadFile] Erro ao fazer upload simples:', error);
          return throwError(
            () =>
              new Error(
                error?.error?.message || 'Falha ao fazer upload do arquivo. Tente novamente.'
              )
          );
        }),
        finalize(() => console.log('[UploadFile] Upload simples concluído')),
        switchMap(() => of(response))
      );
  }

  private handleMultiPartUpload(
    response: GenerateUploadUrlMultiPartResponse,
    file: File
  ): Observable<GenerateUploadUrlResponse> {
    console.log('[UploadFile] URL de upload multiparte gerada com sucesso:', response);

    // TODO: Implementar upload multipart quando necessário
    // Por agora, retorna erro informando que ainda não é suportado
    return throwError(
      () =>
        new Error('Upload multipart ainda não é suportado. Use o modo SIMPLE.')
    );
  }
}
