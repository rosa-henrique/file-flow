using System.Text.Json.Serialization;

using FileFlow.Api;
using FileFlow.Application;
using FileFlow.Application.Commands.CancelMultiPartUpload;
using FileFlow.Application.Commands.CompleteMultiPartUpload;
using FileFlow.Application.Commands.CreateUploadBatch;
using FileFlow.Application.Commands.GenerateUploadUrl;
using FileFlow.Application.Commands.ReprocessUploadBatch;
using FileFlow.Application.Queries.GetUploadBatchById;
using FileFlow.Application.Queries.GetUploadBatches;
using FileFlow.Application.Queries.GetUploadBatchStatus;
using FileFlow.Data;

using MediatR;

using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi()
    .ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.AddServiceDefaults();

builder.AddDataConfig()
    .AddAmazonS3();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.MapGet("upload-batches", (IMediator mediator, CancellationToken cancellationToken) =>
{
    var request = new GetUploadBatchesQuery();
    return mediator.Send(request, cancellationToken);
});

app.MapGet("upload-batches/{id:guid}", (IMediator mediator, Guid id, CancellationToken cancellationToken) =>
{
    var request = new GetUploadBatchByIdQuery(id);
    return mediator.Send(request, cancellationToken);
});

app.MapGet("upload-batches/{id:guid}/status", (IMediator mediator, Guid id, CancellationToken cancellationToken) =>
{
    var request = new GetUploadBatchStatusQuery(id);
    return mediator.Send(request, cancellationToken);
});

app.MapPost("upload-batches/{id:guid}/reprocess", (IMediator mediator, Guid id, CancellationToken cancellationToken) =>
{
    var request = new ReprocessUploadBatchCommand(id);
    return mediator.Send(request, cancellationToken);
});

app.MapPost("upload-batches", async ([FromBody] CreateUploadBatchCommand request, IMediator mediator, CancellationToken cancellationToken) =>
{
    var uploadBatchId = await mediator.Send(request, cancellationToken);

    return Results.Accepted($"/upload-batch/{uploadBatchId}", uploadBatchId);
});

app.MapPost("file/generate-upload-url", ([FromBody] GenerateUploadUrlCommand request, IMediator mediator, CancellationToken cancellationToken)
    => mediator.Send(request, cancellationToken));

app.MapPost("file/complete-multipart-upload", ([FromBody] CompleteMultiPartUploadCommand request, IMediator mediator, CancellationToken cancellationToken)
    => mediator.Send(request, cancellationToken));

app.MapDelete("file/cancel-multipart-upload/{objectKey}/{uploadId}",
    async (string objectKey, string uploadId, IMediator mediator, CancellationToken cancellationToken)
        =>
    {
        var request = new CancelMultiPartUploadCommand(uploadId, objectKey);
        await mediator.Send(request, cancellationToken);
    });

app.Run();

