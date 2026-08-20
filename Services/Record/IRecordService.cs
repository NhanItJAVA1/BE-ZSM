using BE_ZSM.DTOs.Records;
using System.Security.Claims;

namespace BE_ZSM.Services.Interfaces;

public interface IRecordService
{
    Task<List<RecordResponseDto>> GetRecordsAsync();

    Task<RecordResponseDto> GetRecordAsync(int id);

    Task<List<RecordResponseDto>> GetRecordsByUserAsync(int userId);

    Task<List<RecordResponseDto>> GetPendingRecordsAsync(ClaimsPrincipal user);

    Task ApproveRecordAsync(int id,ClaimsPrincipal user);

    Task RejectRecordAsync(int id,string? reason,ClaimsPrincipal user);

    Task<RecordVideoDirectUploadResponseDto> UploadVideoAsync(VideoUploadFormDto form);

    RecordVideoUploadResponseDto CreateVideoUploadUrl(CreateRecordVideoUploadDto dto);

    Task CreateRecordAsync(CreateRecordDto dto, ClaimsPrincipal user);

    Task UpdateRecordAsync(int id,CreateRecordDto dto);

    Task DeleteRecordAsync(int id);

    Task<List<RecordRecommendationDto>> GetRecommendationVehiclesAsync(int mapId);
}