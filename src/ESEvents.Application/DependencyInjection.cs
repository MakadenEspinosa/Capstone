using ESEvents.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ESEvents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
