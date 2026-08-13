namespace TTSmartEcom.Api.Configuration;

public sealed class FrontendHostingOptions
{
    public const string SectionName = "FrontendHosting";

    public bool Enabled { get; init; } = true;

    public string CustomerDistPath { get; init; } = "../../../fe/dist";

    public string AdminDistPath { get; init; } = "../../../ad/dist";
}
