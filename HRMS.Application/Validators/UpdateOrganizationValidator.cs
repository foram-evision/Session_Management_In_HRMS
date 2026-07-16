using FluentValidation;
using HRMS.Application.DTOs.Organization;

namespace HRMS.Application.Validators;

public class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationDto>
{
    public UpdateOrganizationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization Name is required.")
            .MaximumLength(200).WithMessage("Organization Name must not exceed 200 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.");
    }
}
