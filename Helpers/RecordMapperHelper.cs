using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;
using BE_ZSM.Services;

namespace BE_ZSM.Helpers
{
    public class RecordMapperHelper
    {
        private readonly S3PresignedUrlService _presignedUrlService;

        public RecordMapperHelper(S3PresignedUrlService presignedUrlService)
        {
            _presignedUrlService = presignedUrlService;
        }

        public RecordResponseDto? MapToResponseDto(Record? record)
        {
            if (record == null)
                return null;

            return new RecordResponseDto
            {
                Id = record.Id,
                Title = record.Title,
                VideoUrl = _presignedUrlService.CreateGetUrlFromStoredUrl(
                        record.VideoUrl,
                        expiresMinutes: 60)
                    ?? record.VideoUrl,
                ThumbnailUrl = _presignedUrlService.CreateGetUrlFromStoredUrl(record.ThumbnailUrl),
                FinishTime = record.FinishTime.TotalSeconds,
                Description = record.Description,
                Views = record.Views,
                Status = record.Status,
                RejectReason = record.RejectReason,
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
                    ImageUrl = _presignedUrlService.CreateGetUrlFromStoredUrl(record.Map.ImageUrl)
                } : null,
                Vehicle = record.Vehicle != null ? new VehicleMinimalDto
                {
                    Id = record.Vehicle.Id,
                    Name = record.Vehicle.Name,
                    Rank = record.Vehicle.Rank,
                    Type = record.Vehicle.Type,
                    ImageUrl = _presignedUrlService.CreateGetUrlFromStoredUrl(record.Vehicle.ImageUrl)
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
