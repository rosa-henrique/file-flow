using System.Text.Json.Serialization;

using FileFlow.Api;
using FileFlow.Application;
using FileFlow.Application.Commands.CreateUploadBatch;
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

builder.Services.AddApplication();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("upload-batches", (IMediator mediator) =>
{
    var request = new GetUploadBatchesQuery();
    return mediator.Send(request);
});

app.MapPost("upload-batches", async ([FromBody] CreateUploadBatchRequest CreateUploadBatchRequest, IMediator mediator) =>
{
    var uploadBatchId = await mediator.Send(CreateUploadBatchRequest);

    return Results.Created($"/upload-batches/{uploadBatchId}", uploadBatchId);
});

app.Run();

