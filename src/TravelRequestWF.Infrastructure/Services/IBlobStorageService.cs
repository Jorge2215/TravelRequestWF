namespace TravelRequestWF.Infrastructure.Services;

public interface IBlobStorageService
{
    Task<string> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
}
