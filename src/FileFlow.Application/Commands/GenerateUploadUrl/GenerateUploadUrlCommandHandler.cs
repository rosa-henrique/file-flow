using Amazon.S3;
using Amazon.S3.Model;

using FileFlow.Application.Commons;

using MediatR;

using Microsoft.Extensions.Configuration;

namespace FileFlow.Application.Commands.GenerateUploadUrl;

public class GenerateUploadUrlCommandHandler(IAmazonS3 amazonS3Client, IConfiguration configuration) : IRequestHandler<GenerateUploadUrlCommand, GenerateUploadUrlResponse>
{
    private readonly string _bucketTemporary = configuration.GetValue<string>("S3:BucketTemporary")!;

    public async Task<GenerateUploadUrlResponse> Handle(GenerateUploadUrlCommand request, CancellationToken cancellationToken)
    {
        var objectKey = GenerateObjectKey(request.ContentType);

        // Upload simples
        if (request.FileSize < FileConstants.MultipartThreshold)
        {
            return await GenerateSimpleUploadAsync(
                request,
                objectKey);
        }

        // Multipart upload
        return await GenerateMultipartUploadAsync(
            request,
            objectKey,
            cancellationToken);
    }

    /// <summary>
    /// Gera upload simples usando uma única pre-signed URL.
    /// Ideal para arquivos pequenos.
    /// </summary>
    private async Task<GenerateUploadUrlSimpleResponse>
        GenerateSimpleUploadAsync(
            GenerateUploadUrlCommand request,
            string objectKey)
    {
        var preSignedUrlRequest = CreatePreSignedRequest(
            request,
            objectKey,
            expiration: TimeSpan.FromMinutes(15));

        var preSignedUrl =
            await amazonS3Client.GetPreSignedURLAsync(preSignedUrlRequest);

        return new GenerateUploadUrlSimpleResponse(preSignedUrl);
    }

    /// <summary>
    /// Gera multipart upload para arquivos grandes.
    /// </summary>
    private async Task<GenerateUploadUrlMultiPartResponse>
        GenerateMultipartUploadAsync(
            GenerateUploadUrlCommand request,
            string objectKey,
            CancellationToken cancellationToken)
    {
        var uploadId = await InitiateMultipartUploadAsync(
            request,
            objectKey,
            cancellationToken);

        var (partSize, partCount) =
            CalculateParts(request.FileSize);

        var fileUrls = await GenerateMultipartUrlsAsync(
            request,
            objectKey,
            uploadId,
            partSize,
            partCount);

        return new GenerateUploadUrlMultiPartResponse(
            uploadId,
            partSize,
            fileUrls);
    }

    /// <summary>
    /// Inicia o multipart upload no S3.
    /// </summary>
    private async Task<string> InitiateMultipartUploadAsync(
        GenerateUploadUrlCommand request,
        string objectKey,
        CancellationToken cancellationToken)
    {
        var initiateRequest = new InitiateMultipartUploadRequest
        {
            BucketName = _bucketTemporary,
            Key = objectKey,
            ContentType = request.ContentType,
        };

        var response =
            await amazonS3Client.InitiateMultipartUploadAsync(
                initiateRequest,
                cancellationToken);

        return response.UploadId!;
    }

    /// <summary>
    /// Cria um request padrão de pre-signed URL.
    /// </summary>
    private GetPreSignedUrlRequest CreatePreSignedRequest(
        GenerateUploadUrlCommand request,
        string objectKey,
        TimeSpan expiration,
        string? uploadId = null,
        int? partNumber = null)
    {
        return new GetPreSignedUrlRequest
        {
            BucketName = _bucketTemporary,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
            ContentType = request.ContentType,
            Protocol = Protocol.HTTPS,
            UploadId = uploadId,
            PartNumber = partNumber,
        };
    }

    /// <summary>
    /// Calcula o tamanho real da parte atual.
    /// A última parte pode ser menor.
    /// </summary>
    private static long CalculateCurrentPartSize(
        long totalSize,
        long partSize,
        int partCount,
        int partNumber)
    {
        if (partNumber == partCount)
        {
            return totalSize - (partSize * (partCount - 1));
        }

        return partSize;
    }

    /// <summary>
    /// Gera a key única do arquivo.
    /// </summary>
    private static string GenerateObjectKey(
        string contentType)
    {
        var extension =
            FileConstants.AllowedContentTypes[contentType];

        return $"{Guid.NewGuid():N}.{extension}";
    }

    /// <summary>
    /// Gera todas as URLs das partes do multipart upload.
    /// </summary>
    private async Task<List<FileUrlResponse>>
        GenerateMultipartUrlsAsync(
            GenerateUploadUrlCommand request,
            string objectKey,
            string uploadId,
            long partSize,
            int partCount)
    {
        var tasks = Enumerable.Range(1, partCount)
            .Select(async partNumber =>
            {
                var currentPartSize = CalculateCurrentPartSize(
                    request.FileSize,
                    partSize,
                    partCount,
                    partNumber);

                var preSignedUrlRequest = CreatePreSignedRequest(
                    request,
                    objectKey,
                    expiration: TimeSpan.FromHours(6),
                    uploadId: uploadId,
                    partNumber: partNumber);

                var preSignedUrl =
                    await amazonS3Client.GetPreSignedURLAsync(
                        preSignedUrlRequest);

                return new FileUrlResponse(
                    partNumber,
                    currentPartSize,
                    preSignedUrl);
            });

        return [.. await Task.WhenAll(tasks)];
    }

    /// <summary>
    /// Calcula o tamanho ideal das partes para um multipart upload.
    ///
    /// Regras consideradas:
    /// - Cada parte deve ter no mínimo 5MB (limitação do S3)
    /// - O upload pode ter no máximo 10.000 partes
    /// - Limitamos cada parte em no máximo 100MB para evitar chunks gigantes.
    /// </summary>
    /// <param name="totalSize">
    /// Tamanho total do arquivo em bytes.
    /// </param>
    /// <returns>
    /// Tuple contendo:
    /// - PartSize  => tamanho de cada parte em bytes
    /// - PartCount => quantidade total de partes.
    /// </returns>
    private static (long PartSize, int PartCount) CalculateParts(long totalSize)
    {
        // Calcula o tamanho mínimo necessário da parte
        // para não ultrapassar 10.000 partes
        //
        // Exemplo:
        // 500GB / 10000 ≈ 50MB
        //
        // Também garante que nunca fique abaixo de 5MB
        var partSize = Math.Max(FileConstants.MinPartSize, totalSize / FileConstants.MaxParts);

        // Garante que o tamanho da parte
        // nunca ultrapasse o máximo configurado
        partSize = Math.Min(partSize, FileConstants.MaxPartSize);

        // Calcula quantas partes serão necessárias
        //
        // Ceiling arredonda para cima
        //
        // Exemplo:
        // 11MB / 5MB = 2.2
        // Resultado: 3 partes
        var partCount = (int)Math.Ceiling((double)totalSize / partSize);

        // Segurança:
        // garante que nunca ultrapassaremos
        // o limite permitido pelo S3
        if (partCount > FileConstants.MaxParts)
        {
            throw new InvalidOperationException(
                "File is too large for multipart upload configuration.");
        }

        return (partSize, partCount);
    }
}