using FileFlow.Api;
using FileFlow.Application;
using FileFlow.Application.Queries.GetUploadBatches;
using FileFlow.Data;

using MediatR;

var myAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins("http://example.com",
                "http://www.contoso.com");
        });
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

app.UseCors(myAllowSpecificOrigins);

app.MapGet("upload-batches", (IMediator mediator) =>
{
    var request = new GetUploadBatchesQuery();
    return mediator.Send(request);
});

app.Run();

