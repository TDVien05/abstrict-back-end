namespace Abstrict.Api.Options;

public sealed class KycOptions
{
    public const string SectionName = "Kyc";

    public List<BankOption> Banks { get; set; } = [];
}

public sealed class BankOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
