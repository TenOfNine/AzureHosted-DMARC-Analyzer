namespace DmarcAnalyzer.Infrastructure.Ingestion;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public int PollIntervalMinutes { get; set; } = 15;
}
