namespace RealEstate.Application.Common.Files;

public sealed record UploadedFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
