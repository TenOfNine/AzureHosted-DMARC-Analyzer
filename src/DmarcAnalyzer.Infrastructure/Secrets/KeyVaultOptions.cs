namespace DmarcAnalyzer.Infrastructure.Secrets;

public class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public string? Uri { get; set; }
}
