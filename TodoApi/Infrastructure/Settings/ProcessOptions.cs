namespace TodoApi.Infrastructure.Settings;

public sealed class ProcessOptions
{
    public const string SectionName = "Process";

    public int BatchSizeOutbound { get; set; } = 10;

    public int BatchSizeInbound { get; set; } = 10;
}
