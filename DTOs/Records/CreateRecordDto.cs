namespace BE_ZSM.DTOs.Records
{
    public class CreateRecordDto
    {
        public int UserId { get; set; }
        public int MapId { get; set; }
        public int VehicleId { get; set; }
        public int GameModeId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string VideoUrl { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public TimeSpan FinishTime { get; set; }

        public string? Description { get; set; }
    }
}