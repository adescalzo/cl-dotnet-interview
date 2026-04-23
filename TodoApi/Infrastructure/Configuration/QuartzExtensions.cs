using Microsoft.Extensions.Configuration;
using Quartz;
using TodoApi.Application.Jobs;

namespace TodoApi.Infrastructure.Configuration;

public static class QuartzExtensions
{
    private const string OutboundSyncDefault = "0/30 * * * * ?";
    private const string InboundSyncDefault = "0 0/5 * * * ?";

    public static IServiceCollection AddQuartzScheduler(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddQuartz(q =>
        {
            var outboundCron =
                configuration["Jobs:OutboundSyncJob:CronExpression"] ?? OutboundSyncDefault;
            var inboundCron =
                configuration["Jobs:InboundSyncJob:CronExpression"] ?? InboundSyncDefault;

            q.AddJobAndTrigger<OutboundSyncJob>("OutboundSyncJob", outboundCron);
            q.AddJobAndTrigger<InboundSyncJob>("InboundSyncJob", inboundCron);
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
