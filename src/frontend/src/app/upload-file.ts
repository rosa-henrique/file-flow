import { HttpClient, HttpRequest, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, Observable, throwError, switchMap, of, finalize, forkJoin } from 'rxjs';

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

export interface FileUrlPart {
  partNumber: number;
  partSize: number;
  preSignedUrl: string;
}

export interface GenerateUploadUrlMultiPartResponse extends GenerateUploadUrlResponse {
  uploadId: string;
  objectKey: string;
  partSize: number; // Tamanho de cada parte (ex: 5MB)
  fileUrls: FileUrlPart[];
}

export interface CompleteMultipartUploadRequest {
  uploadId: string;
  objectKey: string;
  eTags: Array<{
    partNumber: number;
    eTag: string;
  }>;
}

export interface PartUploadResult {
  partNumber: number;
  eTag: string;
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
    console.log('[UploadFile] Iniciando upload multiparte:', response);

    // Divide o arquivo em chunks
    const chunks = this.divideFileIntoChunks(file, response.partSize);
    console.log(
      `[UploadFile] Arquivo dividido em ${chunks.length} parte(s)`
    );

    // Pega o content type original do arquivo
    const contentType = file.type || 'application/octet-stream';

    // Faz upload de todas as partes em paralelo
    const uploadTasks = chunks.map((chunk, index) => {
      const fileUrl = response.fileUrls[index];
      if (!fileUrl) {
        return throwError(
          () => new Error(`Não há URL pré-assinada para a parte ${index + 1}`)
        );
      }

      return this.uploadFilePart(
        fileUrl.preSignedUrl,
        chunk,
        fileUrl.partNumber,
        contentType
      );
    });

    return forkJoin(uploadTasks).pipe(
      switchMap((results: PartUploadResult[]) => {
        // Após todos os uploads, completa o multipart
        const completeRequest: CompleteMultipartUploadRequest = {
          uploadId: response.uploadId,
          objectKey: response.objectKey,
          eTags: results.map((r) => ({
            partNumber: r.partNumber,
            eTag: r.eTag,
          })),
        };

        console.log('[UploadFile] Completando upload multiparte:', completeRequest);
        return this.completeMultipartUpload(completeRequest, response);
      }),
      catchError((error) => {
        console.error('[UploadFile] Erro ao fazer upload multiparte:', error);
        // Ao detectar erro, cancela o upload multiparte
        return this.cancelMultipartUpload(response.objectKey, response.uploadId).pipe(
          switchMap(() => {
            return throwError(
              () =>
                new Error(
                  error?.message ||
                    'Falha ao fazer upload multiparte do arquivo. Tente novamente.'
                )
            );
          }),
          catchError((cancelError) => {
            console.error(
              '[UploadFile] Erro ao cancelar upload multiparte:',
              cancelError
            );
            return throwError(
              () =>
                new Error(
                  error?.message ||
                    'Falha ao fazer upload multiparte do arquivo. Tente novamente.'
                )
            );
          })
        );
      }),
      finalize(() => console.log('[UploadFile] Upload multiparte finalizado'))
    );
  }

  /**
   * Divide o arquivo em chunks de acordo com o tamanho especificado
   */
  private divideFileIntoChunks(file: File, chunkSize: number): Blob[] {
    const chunks: Blob[] = [];
    let offset = 0;

    while (offset < file.size) {
      const end = Math.min(offset + chunkSize, file.size);
      chunks.push(file.slice(offset, end));
      offset = end;
    }

    return chunks;
  }

  /**
   * Faz upload de uma única parte do arquivo
   */
  private uploadFilePart(
    preSignedUrl: string,
    chunk: Blob,
    partNumber: number,
    contentType: string
  ): Observable<PartUploadResult> {
    console.log(`[UploadFile] Fazendo upload da parte ${partNumber}`);

    const headers = new HttpHeaders({
      'Content-Type': contentType,
    });

    const request = new HttpRequest('PUT', preSignedUrl, chunk, {
      headers,
      reportProgress: true,
    });

    return this.httpClient
      .request<void>(request)
      .pipe(
        switchMap((event: any) => {
          // Captura o eTag do header da resposta
          if (event.type === 4) { // HttpResponse
            const eTag = event.headers.get('etag');
            if (!eTag) {
              return throwError(
                () => new Error(`ETag não encontrado na resposta para a parte ${partNumber}`)
              );
            }
            console.log(
              `[UploadFile] Parte ${partNumber} enviada com sucesso. ETag: ${eTag}`
            );
            return of({ partNumber, eTag });
          }
          return of(null as any);
        }),
        catchError((error) => {
          console.error(
            `[UploadFile] Erro ao fazer upload da parte ${partNumber}:`,
            error
          );
          return throwError(
            () =>
              new Error(
                `Falha ao fazer upload da parte ${partNumber}. ${error.message}`
              )
          );
        })
      );
  }

  /**
   * Completa o upload multiparte enviando todos os eTags
   */
  private completeMultipartUpload(
    request: CompleteMultipartUploadRequest,
    originalResponse: GenerateUploadUrlMultiPartResponse
  ): Observable<GenerateUploadUrlResponse> {
    return this.httpClient
      .post<void>(`${this.apiUrl}/complete-multipart-upload`, request)
      .pipe(
        switchMap(() => {
          console.log('[UploadFile] Upload multiparte completado com sucesso');
          // Retorna a resposta original para manter a consistência com o fluxo SIMPLE
          return of(originalResponse);
        }),
        catchError((error) => {
          console.error('[UploadFile] Erro ao completar upload multiparte:', error);
          return throwError(
            () =>
              new Error(
                error?.error?.message ||
                  'Falha ao completar upload multiparte. Tente novamente.'
              )
          );
        })
      );
  }

  /**
   * Cancela o upload multiparte e deleta dados no backend
   */
  private cancelMultipartUpload(
    objectKey: string,
    uploadId: string
  ): Observable<void> {
    const cancelUrl = `${this.apiUrl}/cancel-multipart-upload/${objectKey}/${uploadId}`;
    console.log('[UploadFile] Cancelando upload multiparte:', cancelUrl);

    return this.httpClient.delete<void>(cancelUrl).pipe(
      switchMap(() => {
        console.log('[UploadFile] Upload multiparte cancelado com sucesso');
        return of(void 0);
      }),
      catchError((error) => {
        console.error('[UploadFile] Erro ao cancelar upload multiparte:', error);
        // Mesmo se falhar ao cancelar, continua com o erro original
        return of(void 0);
      })
    );
  }
}
