namespace DmarcAnalyzer.Infrastructure.Retention;

public class RetentionOptions
{
    public const string SectionName = "Retention";

    public int PurgeIntervalHours { get; set; } = 24;
}
