using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;

namespace BE_ZSM.Helpers
{
    public class RecordHelper
    {
        public void ApplyRecordData(Record record, CreateRecordDto dto)
        {
            record.UserId = dto.UserId;
            record.MapId = dto.MapId;
            record.GameModeId = dto.GameModeId;
            record.VehicleId = dto.VehicleId;
            record.Title = dto.Title;
            record.VideoUrl = dto.VideoUrl;
            record.ThumbnailUrl = dto.ThumbnailUrl;
            record.FinishTime = dto.FinishTime;
            record.Description = dto.Description;
        }
    }
}

