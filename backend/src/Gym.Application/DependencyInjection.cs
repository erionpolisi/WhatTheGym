using System.Reflection;
using FluentValidation;
using Gym.Application.Abstractions;
using Gym.Application.Features.Reviews;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Application;

public static class DependencyInjection
{
    /// <summary>Registers all command/query handlers, validators and application services by convention.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        var handlerInterfaces = new[]
        {
            typeof(ICommandHandler<>),
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>),
        };

        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                if (handlerInterfaces.Contains(definition))
                {
                    services.AddScoped(contract, type);
                }
                else if (definition == typeof(IValidator<>))
                {
                    services.AddSingleton(contract, type);
                }
            }
        }

        services.AddScoped<GymScoreUpdater>();
        return services;
    }
}
