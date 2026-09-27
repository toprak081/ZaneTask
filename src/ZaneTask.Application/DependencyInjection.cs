using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZaneTask.Application.Projects;
using ZaneTask.Application.Tasks;

namespace ZaneTask.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ProjectService>();
        services.AddScoped<TaskService>();
        services.AddScoped<CommentService>();
        return services;
    }
}
