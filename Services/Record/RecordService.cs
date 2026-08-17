using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using BE_ZSM.Repositories.Vehicle;
using BE_ZSM.Services.Interfaces;
using System.Security.Claims;

namespace BE_ZSM.Services;

public class RecordService : IRecordService
{
    private readonly IRecordRepository _recordRepository;
    private readonly RecordHelper _recordHelper;
    private readonly RecordMapperHelper _recordMapperHelper;
    private readonly S3PresignedUrlService _s3PresignedUrlService;
    private readonly AdminAccessHelper _adminAccessHelper;
    private readonly IVehicleRepository _vehicleRepository;

    public RecordService(
        IRecordRepository recordRepository,
        RecordHelper recordHelper,
        RecordMapperHelper recordMapperHelper,
        S3PresignedUrlService s3PresignedUrlService,
        IVehicleRepository vehicleRepository,
        AdminAccessHelper adminAccessHelper)
    {
        _recordRepository = recordRepository;
        _recordHelper = recordHelper;
        _recordMapperHelper = recordMapperHelper;
        _s3PresignedUrlService = s3PresignedUrlService;
        _vehicleRepository = vehicleRepository;
        _adminAccessHelper = adminAccessHelper;
    }

    public async Task<List<RecordResponseDto>> GetRecordsAsync()
    {
        var records =
            await _recordRepository.GetAllApprovedAsync();

        return _recordMapperHelper
            .MapToResponseDtos(records);
    }

    public async Task<RecordResponseDto> GetRecordAsync(int id)
    {
        var record =
            await _recordRepository.GetByIdAsync(id);

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        return _recordMapperHelper
            .MapToResponseDto(record);
    }

    public async Task<List<RecordResponseDto>> GetRecordsByUserAsync(
        int userId)
    {
        var records =
            await _recordRepository.GetByUserIdAsync(userId);

        return _recordMapperHelper
            .MapToResponseDtos(records);
    }
    public async Task<List<RecordResponseDto>> GetPendingRecordsAsync(
    ClaimsPrincipal user)
    {
        await EnsureAdminAsync(user);

        var records =
            await _recordRepository.GetPendingAsync();

        return _recordMapperHelper
            .MapToResponseDtos(records);
    }
    public async Task ApproveRecordAsync(
    int id,
    ClaimsPrincipal user)
    {
        await EnsureAdminAsync(user);

        var record =
            await _recordRepository.GetEntityByIdAsync(id);

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        record.Status = Enums.RecordStatus.Approved;
        record.ReviewedAt = DateTime.UtcNow;

        var adminId =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(adminId, out var parsedAdminId))
        {
            record.ReviewedBy = parsedAdminId;
        }

        await _recordRepository.SaveChangesAsync();
    }
    public async Task RejectRecordAsync(
    int id,
    string? reason,
    ClaimsPrincipal user)
    {
        await EnsureAdminAsync(user);

        var record =
            await _recordRepository.GetEntityByIdAsync(id);

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        record.Status = Enums.RecordStatus.Rejected;

        record.RejectReason =
            string.IsNullOrWhiteSpace(reason)
                ? null
                : reason.Trim();

        record.ReviewedAt = DateTime.UtcNow;

        var adminId =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(adminId, out var parsedAdminId))
        {
            record.ReviewedBy = parsedAdminId;
        }

        await _recordRepository.SaveChangesAsync();
    }

    public async Task<RecordResponseDto> CreateRecordAsync(
    CreateRecordDto dto)
    {
        var record = new Record();

        _recordHelper.ApplyRecordData(record, dto);

        record.Views = 0;
        record.CreatedAt = DateTime.UtcNow;
        record.UpdatedAt = DateTime.UtcNow;

        await _recordRepository.AddAsync(record);

        await _recordRepository.SaveChangesAsync();

        var savedRecord =
            await _recordRepository.GetByIdAsync(record.Id);

        if (savedRecord == null)
        {
            throw new AppException(
                "Failed to load created record",
                500,
                "RECORD_LOAD_FAILED");
        }

        return _recordMapperHelper
            .MapToResponseDto(savedRecord);
    }
    public async Task<RecordResponseDto> UpdateRecordAsync(
    int id,
    CreateRecordDto dto)
    {
        var record =
            await _recordRepository.GetEntityByIdAsync(id);

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        _recordHelper.ApplyRecordData(record, dto);

        record.UpdatedAt = DateTime.UtcNow;

        await _recordRepository.SaveChangesAsync();

        var updatedRecord =
            await _recordRepository.GetByIdAsync(id);

        if (updatedRecord == null)
        {
            throw new AppException(
                "Failed to load updated record",
                500,
                "RECORD_LOAD_FAILED");
        }

        return _recordMapperHelper
            .MapToResponseDto(updatedRecord);
    }
    private async Task EnsureAdminAsync(
    ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedException(
                "Authentication required",
                "AUTHENTICATION_REQUIRED");
        }

        var isAdmin =
            await _adminAccessHelper.IsCurrentUserAdminAsync(user);

        if (!isAdmin)
        {
            throw new ForbiddenException(
                "Admin access required",
                "ADMIN_ACCESS_REQUIRED");
        }
    }
    public async Task DeleteRecordAsync(int id)
    {
        var record =
            await _recordRepository.GetEntityByIdAsync(id);

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        _recordRepository.Delete(record);

        await _recordRepository.SaveChangesAsync();
    }
    public async Task<RecordVideoDirectUploadResponseDto> UploadVideoAsync(
    VideoUploadFormDto form)
    {
        if (form?.VideoFile == null || form.VideoFile.Length == 0)
        {
            throw new BadRequestException(
                "VideoFile is required",
                "VIDEO_FILE_REQUIRED");
        }

        var result =
            await _s3PresignedUrlService.UploadVideoAsync(
                form.VideoFile);

        return new RecordVideoDirectUploadResponseDto
        {
            ObjectKey = result.ObjectKey,
            PublicUrl = result.PublicUrl,
            UploadedAtUtc = result.UploadedAtUtc
        };
    }
    public RecordVideoUploadResponseDto CreateVideoUploadUrl(
    CreateRecordVideoUploadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FileName))
        {
            throw new BadRequestException(
                "FileName is required",
                "FILE_NAME_REQUIRED");
        }

        var contentType =
            string.IsNullOrWhiteSpace(dto.ContentType)
                ? "video/mp4"
                : dto.ContentType;

        var result =
            _s3PresignedUrlService.CreateVideoUploadUrl(
                dto.FileName,
                contentType);

        return new RecordVideoUploadResponseDto
        {
            UploadUrl = result.UploadUrl,
            ObjectKey = result.ObjectKey,
            PublicUrl = result.PublicUrl,
            ExpiresAtUtc = result.ExpiresAtUtc
        };
    }

    public async Task<List<RecordRecommendationDto>>
    GetRecommendationVehiclesAsync(int mapId)
    {
        var records =
            await _recordRepository.GetApprovedByMapIdAsync(mapId);

        var vehicles = records
            .GroupBy(r => r.VehicleId)
            .Select(g => new
            {
                VehicleId = g.Key,

                BestTime = g.Min(x => x.FinishTime),

                AverageTime = TimeSpan.FromMilliseconds(
                    g.Average(x =>
                        x.FinishTime.TotalMilliseconds)),

                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var vehicleIds = vehicles
            .Select(v => v.VehicleId)
            .ToList();

        var vehicleEntities = await _vehicleRepository.GetByIdsAsync(vehicleIds);

        return vehicles
            .Select(v =>
            {
                var vehicle =
                    vehicleEntities.FirstOrDefault(
                        x => x.Id == v.VehicleId);

                return new RecordRecommendationDto
                {
                    VehicleId = v.VehicleId,
                    VehicleName = vehicle?.Name,
                    Count = v.Count,
                    BestTime = v.BestTime,
                    AverageTime = v.AverageTime
                };
            })
            .ToList();
    }
}