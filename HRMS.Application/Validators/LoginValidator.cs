using FluentValidation;
using HRMS.Application.DTOs.Auth;

namespace HRMS.Application.Validators;

public class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .Matches(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")
            .WithMessage("Please enter a valid email address (e.g. user@example.com).");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
