using FileFlow.Data.Context;
using FileFlow.Data.Entities;
using FileFlow.Shared.Exceptions;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace FileFlow.Application.Queries.GetUploadBatchStatus;

public class GetUploadBatchStatusQueryHandler(FileFlowDbContext dbContext) : IRequestHandler<GetUploadBatchStatusQuery, GetUploadBatchStatusResponse>
{
    private const int TimeoutSeconds = 120;

    public async Task<GetUploadBatchStatusResponse> Handle(GetUploadBatchStatusQuery request, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (true)
            {
                var uploadBatch = await dbContext.UploadBatches
                    .AsNoTracking()
                    .Where(x => x.Id == request.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Status,
                    })
                    .SingleOrDefaultAsync(linkedCts.Token);

                if (uploadBatch is null)
                {
                    throw new NotFoundException(
                        $"Não foi encontrado batch para id {request.Id}");
                }

                if (uploadBatch.Status is UploadBatchStatus.COMPLETED
                    or UploadBatchStatus.FAILED
                    or UploadBatchStatus.PARTIAL)
                {
                    return new GetUploadBatchStatusResponse(
                        uploadBatch.Id,
                        uploadBatch.Status);
                }

                await timer.WaitForNextTickAsync(linkedCts.Token);
            }
        }
        catch (OperationCanceledException) when (
            timeoutCts.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            return new GetUploadBatchStatusResponse(
                request.Id,
                UploadBatchStatus.PROCESSING,
                true);
        }
    }
}