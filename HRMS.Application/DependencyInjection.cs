using System.Reflection;
using FluentValidation;
using HRMS.Application.Interfaces;
using HRMS.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // ─── Application Services ──────────────────────────────────────────────

        services.AddScoped<IAuthService,         AuthService>();
        services.AddScoped<IEmployeeService,      EmployeeService>();

        services.AddScoped<IOrganizationService,  OrganizationService>();

        return services;
    }
}