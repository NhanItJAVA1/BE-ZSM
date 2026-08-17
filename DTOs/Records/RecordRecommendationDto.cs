namespace BE_ZSM.DTOs.Records;

public class RecordRecommendationDto
{
    public int VehicleId { get; set; }

    public string? VehicleName { get; set; }

    public int Count { get; set; }

    public TimeSpan BestTime { get; set; }

    public TimeSpan AverageTime { get; set; }
}