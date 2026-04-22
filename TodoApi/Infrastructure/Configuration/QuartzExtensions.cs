using Quartz;
using TodoApi.Application.Jobs;

namespace TodoApi.Infrastructure.Configuration;

public static class QuartzExtensions
{
    public static IServiceCollection AddQuartzScheduler(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            q.AddJobAndTrigger<OutboundSyncJob>("OutboundSyncJob", "0/30 * * * * ?");
            q.AddJobAndTrigger<InboundSyncJob>("InboundSyncJob", "0 0/5 * * * ?");
        });

        services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);

        return services;
    }

    private static void AddJobAndTrigger<TJob>(
        this IServiceCollectionQuartzConfigurator q,
        string identity,
        string cronExpression
    )
        where TJob : IJob
    {
        var key = new JobKey(identity);
        q.AddJob<TJob>(opts => opts.WithIdentity(key));
        q.AddTrigger(opts =>
            opts.ForJob(key).WithIdentity($"{identity}-trigger").WithCronSchedule(cronExpression)
        );
    }
}
