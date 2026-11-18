using ESEvents.Application.Chat;
using ESEvents.Application.Companies;
using ESEvents.Application.Departments;
using ESEvents.Application.Events;
using ESEvents.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ESEvents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEventNotifier, EventNotifier>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ICompanyService, CompanyService>();
        return services;
    }
}
