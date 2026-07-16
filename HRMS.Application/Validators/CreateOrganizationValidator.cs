using FluentValidation;
using HRMS.Application.DTOs.Organization;

namespace HRMS.Application.Validators;

public class CreateOrganizationValidator : AbstractValidator<CreateOrganizationDto>
{
    public CreateOrganizationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name cannot exceed 200 characters.")
            .Must(name => name.Any(char.IsLetter))
            .WithMessage("Organization name must contain at least one alphabet.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Organization address is required.")
            .MaximumLength(500).WithMessage("Organization address cannot exceed 500 characters.");
    }
}