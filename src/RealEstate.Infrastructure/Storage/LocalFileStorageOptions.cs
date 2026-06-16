namespace RealEstate.Infrastructure.Storage;

public sealed class LocalFileStorageOptions
{
    public string RootPath { get; set; } = string.Empty;

    public string PublicBasePath { get; set; } = "/uploads";
}