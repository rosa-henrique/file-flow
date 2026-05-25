using FluentValidation;

namespace FileFlow.Application.Commands.CreateUploadBatch;

public class CreateUploadBatchValidator : AbstractValidator<CreateUploadBatchRequest>
{
    public CreateUploadBatchValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);
    }
}