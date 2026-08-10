namespace DmarcAnalyzer.Core.Entities;

/// <summary>Singleton row (Id is always 1) controlling how long report data is kept before purge.</summary>
public class RetentionSettings
{
    public const int SingletonId = 1;
    public const int DefaultRetentionDays = 400;

    public int Id { get; set; } = SingletonId;
    public int RetentionDays { get; set; } = DefaultRetentionDays;
    public DateTime? LastPurgeRunUtc { get; set; }
    public int LastPurgeRowsDeleted { get; set; }
}
