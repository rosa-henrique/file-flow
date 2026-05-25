using FileFlow.Shared.Exceptions;

using FluentValidation;

using MediatR;

namespace FileFlow.Application.Behaviors;

public class ValidationPipelineBehavior<TRequest, TResponse>(IValidator<TRequest>? validator = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (validator is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (validationResult.IsValid)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var errors = validationResult.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).ToArray());

        throw new AppValidationException(errors);
    }
}