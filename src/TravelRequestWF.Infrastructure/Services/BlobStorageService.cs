using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace TravelRequestWF.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly AzureStorageOptions _options;

    public BlobStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
        if (_options.ConnectionString == "YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE" ||
            string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Azure Storage connection string is not configured. Set AzureStorage:ConnectionString in appsettings.json.");
        }
    }

    public async Task<string> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var serviceClient = new BlobServiceClient(_options.ConnectionString);
        var containerClient = serviceClient.GetBlobContainerClient(_options.ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var uniqueName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var blobClient = containerClient.GetBlobClient(uniqueName);

        await blobClient.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        return blobClient.Uri.ToString();
    }
}
