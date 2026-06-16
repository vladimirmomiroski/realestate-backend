namespace RealEstate.Application.Common.Storage;

public sealed record StoredFileResult(
    string OriginalFileName,
    string StoredFileName,
    string ContentType,
    long SizeBytes,
    string Url);