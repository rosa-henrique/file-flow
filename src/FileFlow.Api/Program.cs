using System.Text.Json.Serialization;

using FileFlow.Api;
using FileFlow.Application;
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

builder.Services.AddApplication();

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

app.MapPost("upload-batch", async ([FromBody] CreateUploadBatchCommand createUploadBatchCommand, IMediator mediator) =>
{
    var uploadBatchId = await mediator.Send(createUploadBatchCommand);

    return Results.Created($"/upload-batch/{uploadBatchId}", uploadBatchId);
});

app.MapPost("file/generate-upload-url", ([FromBody] GenerateUploadUrlCommand generateUploadUrlCommand, IMediator mediator)
    => mediator.Send(generateUploadUrlCommand));

app.Run();

