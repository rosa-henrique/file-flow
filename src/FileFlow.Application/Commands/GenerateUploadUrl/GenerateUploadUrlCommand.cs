using MediatR;

namespace FileFlow.Application.Commands.GenerateUploadUrl;

public record GenerateUploadUrlCommand(long FileSize, string ContentType) : IRequest<GenerateUploadUrlResponse>;