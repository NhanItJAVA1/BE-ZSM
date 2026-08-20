using AutoMapper;
using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Helpers;
using BE_ZSM.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BE_ZSM.Extensions;
using BE_ZSM.Repositories.Generic;
namespace BE_ZSM.Services;

public class RecordService : IRecordService
{
    private readonly S3PresignedUrlService _s3PresignedUrlService;
    private readonly AdminAccessHelper _adminAccessHelper;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Record> _recordRepo;

    public RecordService(
        S3PresignedUrlService s3PresignedUrlService,
        AdminAccessHelper adminAccessHelper,
        IMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _s3PresignedUrlService = s3PresignedUrlService;
        _adminAccessHelper = adminAccessHelper;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _recordRepo = _unitOfWork.GetRepository<Record>();
    }

    public async Task<List<RecordResponseDto>> GetRecordsAsync()
    {
        var records = await _recordRepo
            .Where(r => r.Status == Enums.RecordStatus.Approved)
            .IncludeDetails()
            .AsNoTracking()
            .ToListAsync();

        var responses = _mapper.Map<List<RecordResponseDto>>(records);
        foreach (var response in responses)
        {
            response.VideoUrl =
                _s3PresignedUrlService.CreateGetUrlFromStoredUrl(
                    response.VideoUrl);

            response.ThumbnailUrl =
                _s3PresignedUrlService.CreateGetUrlFromStoredUrl(
                    response.ThumbnailUrl);
            if (response.Map != null)
            {
                response.Map.ImageUrl =
                    _s3PresignedUrlService.CreateGetUrlFromStoredUrl(
                        response.Map.ImageUrl);
            }

            if (response.Vehicle != null)
            {
                response.Vehicle.ImageUrl =
                    _s3PresignedUrlService.CreateGetUrlFromStoredUrl(
                        response.Vehicle.ImageUrl);
            }
        }

        return responses;
    }

    public async Task<RecordResponseDto> GetRecordAsync(int id)
    {
        var record = await _recordRepo
            .Where(r => r.Id == id)
            .IncludeDetails()
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        return _mapper.Map<RecordResponseDto>(record);
    }

    public async Task<List<RecordResponseDto>> GetRecordsByUserAsync(int userId)
    {
        var records = await _recordRepo
            .Where(r => r.UserId == userId)
            .IncludeDetails()
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<RecordResponseDto>>(records);
    }
    public async Task<List<RecordResponseDto>> GetPendingRecordsAsync(
    ClaimsPrincipal user)
    {
        await EnsureAdminAsync(user);

        var records = await _recordRepo
            .Where(r => r.Status == Enums.RecordStatus.Pending)
            .IncludeDetails()
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<RecordResponseDto>>(records);
    }
    public async Task ApproveRecordAsync(int id, ClaimsPrincipal user)
    {
        await EnsureAdminAsync(user);

        var record = await _recordRepo
            .Where(r => r.Id == id)
            .IncludeDetails()
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        record.Status = Enums.RecordStatus.Approved;
        record.ReviewedAt = DateTime.UtcNow;

        var adminId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(adminId, out var parsedAdminId))
        {
            record.ReviewedBy = parsedAdminId;
        }

        await _unitOfWork.SaveChangesAsync();
    }
    public async Task RejectRecordAsync(int id, string? reason, ClaimsPrincipal user)
    {
        await EnsureAdminAsync(user);

        var record = await _recordRepo
            .Where(r => r.Id == id)
            .IncludeDetails()
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        record.Status = Enums.RecordStatus.Rejected;

        record.RejectReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        record.ReviewedAt = DateTime.UtcNow;

        var adminId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(adminId, out var parsedAdminId))
        {
            record.ReviewedBy = parsedAdminId;
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CreateRecordAsync(CreateRecordDto dto, ClaimsPrincipal user)
    {
        var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var record = _mapper.Map<Record>(dto);

        record.UserId = userId;
        record.Views = 0;
        record.CreatedAt = DateTime.UtcNow;

        await _recordRepo.CreateAsync(record);
        await _unitOfWork.SaveChangesAsync();

        var savedRecord =
            await _recordRepo
                .Where(r => r.Id == record.Id)
                .IncludeDetails()
                .AsNoTracking()
                .FirstOrDefaultAsync();


        if (savedRecord == null)
        {
            throw new AppException(
                "Failed to load created record",
                500,
                "RECORD_LOAD_FAILED");
        }

    }
    public async Task UpdateRecordAsync(int id, CreateRecordDto dto)
    {
        var record = await _recordRepo.FindAsync(r => r.Id == id);

        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        _mapper.Map(dto, record);

        record.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        var updatedRecord = await _recordRepo
            .Where(r => r.Id == id)
            .IncludeDetails()
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (updatedRecord == null)
        {
            throw new AppException(
                "Failed to load updated record",
                500,
                "RECORD_LOAD_FAILED");
        }

    }
    private async Task EnsureAdminAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedException(
                "Authentication required",
                "AUTHENTICATION_REQUIRED");
        }

        var isAdmin = await _adminAccessHelper.IsCurrentUserAdminAsync(user);

        if (!isAdmin)
        {
            throw new ForbiddenException(
                "Admin access required",
                "ADMIN_ACCESS_REQUIRED");
        }
    }
    public async Task DeleteRecordAsync(int id)
    {
        var repository = _recordRepo;
        var record = await repository.FindAsync(r => r.Id == id);


        if (record == null)
        {
            throw new NotFoundException(
                "Record not found",
                "RECORD_NOT_FOUND");
        }

        await repository.DeleteAsync(record);

        await _unitOfWork.SaveChangesAsync();
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

        var result = await _s3PresignedUrlService.UploadVideoAsync(form.VideoFile);

        return new RecordVideoDirectUploadResponseDto
        {
            ObjectKey = result.ObjectKey,
            PublicUrl = result.PublicUrl,
            UploadedAtUtc = result.UploadedAtUtc
        };
    }
    public RecordVideoUploadResponseDto CreateVideoUploadUrl( CreateRecordVideoUploadDto dto)
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
        var records = await _recordRepo
            .Where(r =>
                r.MapId == mapId &&
                r.Status == Enums.RecordStatus.Approved)
            .ToListAsync();

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

        var vehicleEntities = await _unitOfWork.GetRepository<Entities.Vehicle>()
            .Where(v => vehicleIds.Contains(v.Id))
            .AsNoTracking()
            .ToListAsync();

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