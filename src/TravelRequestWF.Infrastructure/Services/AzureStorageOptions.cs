namespace TravelRequestWF.Infrastructure.Services;

public class AzureStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "travel-documents";
}
