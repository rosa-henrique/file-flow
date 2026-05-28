using FluentValidation;

namespace FileFlow.Application.Commands.GenerateUploadUrl;

public class GenerateUploadUrlValidator : AbstractValidator<GenerateUploadUrlCommand>
{
    public GenerateUploadUrlValidator()
    {
        RuleFor(c => c.FileSize)
            .GreaterThan(0)
            .WithMessage("File size must be greater than zero");
    }
}