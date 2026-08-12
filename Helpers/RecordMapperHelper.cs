using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;

namespace BE_ZSM.Helpers
{
    /// <summary>
    /// Helper to map Record entity to RecordResponseDto
    /// </summary>
    public class RecordMapperHelper
    {
        public RecordResponseDto? MapToResponseDto(Record? record)
        {
            if (record == null)
                return null;

            return new RecordResponseDto
            {
                Id = record.Id,
                Title = record.Title,
                VideoUrl = record.VideoUrl,
                ThumbnailUrl = record.ThumbnailUrl,
                FinishTime = record.FinishTime.TotalSeconds,
                Description = record.Description,
                Views = record.Views,
                CreatedAt = record.CreatedAt,
                UpdatedAt = record.UpdatedAt,
                User = record.User != null ? new UserMinimalDto
                {
                    Id = record.User.Id,
                    Username = record.User.Username,
                    Email = record.User.Email
                } : null,
                Map = record.Map != null ? new MapMinimalDto
                {
                    Id = record.Map.Id,
                    Name = record.Map.Name,
                    Rate = record.Map.Rate.ToString(),
                    ImageUrl = record.Map.ImageUrl
                } : null,
                Vehicle = record.Vehicle != null ? new VehicleMinimalDto
                {
                    Id = record.Vehicle.Id,
                    Name = record.Vehicle.Name,
                    Rank = record.Vehicle.Rank,
                    Type = record.Vehicle.Type,
                    ImageUrl = record.Vehicle.ImageUrl
                } : null,
                GameMode = record.GameMode != null ? new GameModeMinimalDto
                {
                    Id = record.GameMode.Id,
                    Name = record.GameMode.Name,
                    Description = record.GameMode.Description
                } : null
            };
        }

        public List<RecordResponseDto> MapToResponseDtos(List<Record>? records)
        {
            if (records == null || records.Count == 0)
                return new List<RecordResponseDto>();

            var result = new List<RecordResponseDto>();
            foreach (var record in records)
            {
                var dto = MapToResponseDto(record);
                if (dto != null)
                {
                    result.Add(dto);
                }
            }
            return result;
        }
    }
}
