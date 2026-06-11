using System.Text.Json.Serialization;

using FileFlow.Api;
using FileFlow.Application;
using FileFlow.Application.Commands.CancelMultiPartUpload;
using FileFlow.Application.Commands.CompleteMultiPartUpload;
using FileFlow.Application.Commands.CreateUploadBatch;
using FileFlow.Application.Commands.GenerateUploadUrl;
using FileFlow.Application.Queries.GetUploadBatches;
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

app.MapGet("upload-batch", (IMediator mediator) =>
{
    var request = new GetUploadBatchesQuery();
    return mediator.Send(request);
});

app.MapPost("upload-batch", async ([FromBody] CreateUploadBatchCommand request, IMediator mediator) =>
{
    var uploadBatchId = await mediator.Send(request);

    return Results.Created($"/upload-batch/{uploadBatchId}", uploadBatchId);
});

app.MapPost("file/generate-upload-url", ([FromBody] GenerateUploadUrlCommand request, IMediator mediator)
    => mediator.Send(request));

app.MapPost("file/complete-multipart-upload", ([FromBody] CompleteMultiPartUploadCommand request, IMediator mediator)
    => mediator.Send(request));

app.MapDelete("file/cancel-multipart-upload/{objectKey}/{uploadId}",
    async (string objectKey, string uploadId, IMediator mediator)
        =>
    {
        var request = new CancelMultiPartUploadCommand(uploadId, objectKey);
        await mediator.Send(request);
    });

app.Run();

