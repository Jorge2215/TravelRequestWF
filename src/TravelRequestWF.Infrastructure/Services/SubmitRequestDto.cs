namespace TravelRequestWF.Infrastructure.Services;

public record SubmitRequestDto(
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    string Purpose,
    IReadOnlyList<(Stream Stream, string FileName, string ContentType)> Documents
);
