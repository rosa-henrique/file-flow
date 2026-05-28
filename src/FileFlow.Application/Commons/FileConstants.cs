using System.Collections.Frozen;

namespace FileFlow.Application.Commons;

public static class FileConstants
{
    // Tamanho mínimo permitido pelo S3 para multipart upload
    // (exceto a última parte)
    public const long MinPartSize = 5 * 1024 * 1024; // 5MB

    // Limite definido pela aplicação
    // para evitar partes muito grandes
    public const long MaxPartSize = 100 * 1024 * 1024; // 100MB

    // Quantidade máxima de partes permitidas pelo S3
    public const int MaxParts = 10000;

    // Define tamanho máximo do arquivo para upload simples
    public const long MultipartThreshold = 20 * 1024 * 1024;

    public static readonly FrozenDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // PDF
            ["application/pdf"] = "pdf",

            // Images
            ["image/png"] = "png",
            ["image/jpeg"] = "jpg",
            ["image/webp"] = "webp",
            ["image/gif"] = "gif",

            // Videos
            ["video/mp4"] = "mp4",
            ["video/quicktime"] = "mov",
            ["video/x-msvideo"] = "avi",

            // Word
            ["application/msword"] = "doc",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "docx",

            // Excel
            ["application/vnd.ms-excel"] = "xls",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "xlsx",

            // PowerPoint
            ["application/vnd.ms-powerpoint"] = "ppt",
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = "pptx",

            // Text
            ["text/plain"] = "txt",
            ["text/csv"] = "csv",

            // Zip
            ["application/zip"] = "zip",
            ["application/x-zip-compressed"] = "zip",

            // JSON
            ["application/json"] = "json",
        }.ToFrozenDictionary();
}