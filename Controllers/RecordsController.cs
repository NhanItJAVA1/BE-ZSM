using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BE_ZSM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecordsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RecordHelper _recordHelper;
        private readonly DbSaveHelper _dbSaveHelper;
        private readonly S3PresignedUrlService _s3PresignedUrlService;
        private readonly RecordMapperHelper _recordMapperHelper;
        private readonly AdminAccessHelper _adminAccessHelper;

        public RecordsController(
            AppDbContext context,
            RecordHelper recordHelper,
            DbSaveHelper dbSaveHelper,
            S3PresignedUrlService s3PresignedUrlService,
            RecordMapperHelper recordMapperHelper,
            AdminAccessHelper adminAccessHelper)
        {
            _context = context;
            _recordHelper = recordHelper;
            _dbSaveHelper = dbSaveHelper;
            _s3PresignedUrlService = s3PresignedUrlService;
            _recordMapperHelper = recordMapperHelper;
            _adminAccessHelper = adminAccessHelper;
        }

        private async Task<IActionResult?> EnsureAdminAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { message = "Authentication required" });
            }

            if (!await _adminAccessHelper.IsCurrentUserAdminAsync(User))
            {
                return Forbid();
            }

            return null;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecords()
        {
            try
            {
                var records = await _context.Records
                    .Where(r => r.Status == Enums.RecordStatus.Approved)
                    .Include(r => r.User)
                    .Include(r => r.Map)
                    .Include(r => r.GameMode)
                    .Include(r => r.Vehicle)
                    .ToListAsync();

                var dtos = _recordMapperHelper.MapToResponseDtos(records);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRecord(int id)
        {
            var record = await _context.Records
                .Include(r => r.User)
                .Include(r => r.Map)
                .Include(r => r.GameMode)
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null)
            {
                return NotFound(new
                {
                    message = "Record not found"
                });
            }

            var dto = _recordMapperHelper.MapToResponseDto(record);
            return Ok(dto);
        }


        [HttpGet("records-by-user/{userId}")]
        public async Task<IActionResult> GetRecordsByUser(int userId)
        {
            var records = await _context.Records
                .Where(r => r.UserId == userId)
                .Include(r => r.User)
                .Include(r => r.Map)
                .Include(r => r.GameMode)
                .Include(r => r.Vehicle)
                .ToListAsync();

            var dtos = _recordMapperHelper.MapToResponseDtos(records);
            return Ok(dtos);
        }

        [Authorize]
        [HttpGet("admin/records/pending")]
        public async Task<IActionResult> GetPendingRecords()
        {
            var adminCheck = await EnsureAdminAsync();
            if (adminCheck != null)
            {
                return adminCheck;
            }

            try
            {
                var records = await _context.Records
                    .Where(r => r.Status == Enums.RecordStatus.Pending)
                    .Include(r => r.User)
                    .Include(r => r.Map)
                    .Include(r => r.GameMode)
                    .Include(r => r.Vehicle)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                var dtos = _recordMapperHelper.MapToResponseDtos(records);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [Authorize]
        [HttpPut("admin/records/{id}/approve")]
        public async Task<IActionResult> ApproveRecord(int id)
        {
            var adminCheck = await EnsureAdminAsync();
            if (adminCheck != null)
            {
                return adminCheck;
            }

            var record = await _context.Records
                .FirstOrDefaultAsync(r => r.Id == id);
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (record == null)
            {
                return NotFound(new
                {
                    message = "Record not found"
                });
            }
            record.Status = Enums.RecordStatus.Approved;
            record.ReviewedAt = DateTime.UtcNow;
            if (int.TryParse(adminId, out var parsedAdminId))
            {
                record.ReviewedBy = parsedAdminId;
            }
            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }
            return NoContent();
        }

        [Authorize]
        [HttpPut("admin/records/{id}/reject")]
        public async Task<IActionResult> RejectRecord(int id, [FromQuery] string? reason = null)
        {
            var adminCheck = await EnsureAdminAsync();
            if (adminCheck != null)
            {
                return adminCheck;
            }

            var record = await _context.Records
                .FirstOrDefaultAsync(r => r.Id == id);
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (record == null)
            {
                return NotFound(new
                {
                    message = "Record not found"
                });
            }
            record.Status = Enums.RecordStatus.Rejected;
            record.RejectReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            record.ReviewedAt = DateTime.UtcNow;
            if (int.TryParse(adminId, out var parsedAdminId))
            {
                record.ReviewedBy = parsedAdminId;
            }
            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }
            return NoContent();
        }

        [Authorize]
        [HttpPost("video-upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<RecordVideoDirectUploadResponseDto>> UploadVideo(
            [FromForm] VideoUploadFormDto form)
        {
            if (form?.VideoFile == null || form.VideoFile.Length == 0)
            {
                return BadRequest(new
                {
                    message = "VideoFile is required"
                });
            }

            var result = await _s3PresignedUrlService.UploadVideoAsync(form.VideoFile);

            return Ok(new RecordVideoDirectUploadResponseDto
            {
                ObjectKey = result.ObjectKey,
                PublicUrl = result.PublicUrl,
                UploadedAtUtc = result.UploadedAtUtc
            });
        }

        [Authorize]
        [HttpPost("video-upload-url")]
        public ActionResult<RecordVideoUploadResponseDto> CreateVideoUploadUrl(
            [FromBody] CreateRecordVideoUploadDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FileName))
            {
                return BadRequest(new
                {
                    message = "FileName is required"
                });
            }

            var contentType = string.IsNullOrWhiteSpace(dto.ContentType)
                ? "video/mp4"
                : dto.ContentType;

            var result = _s3PresignedUrlService.CreateVideoUploadUrl(
                dto.FileName,
                contentType);

            return Ok(new RecordVideoUploadResponseDto
            {
                UploadUrl = result.UploadUrl,
                ObjectKey = result.ObjectKey,
                PublicUrl = result.PublicUrl,
                ExpiresAtUtc = result.ExpiresAtUtc
            });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateRecord(CreateRecordDto dto)
        {
            var record = new Record();
            _recordHelper.ApplyRecordData(record, dto);
            record.Views = 0;
            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;

            _context.Records.Add(record);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            await _context.Entry(record)
                .Reference(r => r.User).LoadAsync();
            await _context.Entry(record)
                .Reference(r => r.Map).LoadAsync();
            await _context.Entry(record)
                .Reference(r => r.Vehicle).LoadAsync();
            await _context.Entry(record)
                .Reference(r => r.GameMode).LoadAsync();

            var responseDto = _recordMapperHelper.MapToResponseDto(record);
            return CreatedAtAction(
                nameof(GetRecord),
                new { id = record.Id },
                responseDto
            );
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRecord(
            int id,
            CreateRecordDto dto)
        {
            var record = await _context.Records
                .Include(r => r.User)
                .Include(r => r.Map)
                .Include(r => r.GameMode)
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null)
            {
                return NotFound(new
                {
                    message = "Record not found"
                });
            }

            _recordHelper.ApplyRecordData(record, dto);
            record.UpdatedAt = DateTime.UtcNow;

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            var responseDto = _recordMapperHelper.MapToResponseDto(record);
            return Ok(responseDto);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecord(int id)
        {
            var record = await _context.Records
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null)
            {
                return NotFound(new
                {
                    message = "Record not found"
                });
            }

            _context.Records.Remove(record);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return NoContent();
        }

        //GET /api/recommendations/maps/{mapId}/vehicles
        [HttpGet("/recommendations/maps/{mapId}/vehicles")]
        public async Task<IActionResult> GetRecommentdationVehicles(int mapId)
        {
            // 1. Lấy record từ database
            var records = await _context.Records
                .Where(r =>
                    r.MapId == mapId &&
                    r.Status == Enums.RecordStatus.Approved)
                .Select(r => new
                {
                    r.VehicleId,
                    r.FinishTime
                })
                .ToListAsync();

            // 2. Group + tính toán ở C#
            var vehicles = records
                .GroupBy(r => r.VehicleId)
                .Select(g => new
                {
                    VehicleId = g.Key,

                    BestTime = g.Min(x => x.FinishTime),

                    AverageTime = TimeSpan.FromMilliseconds(
                        g.Average(x => x.FinishTime.TotalMilliseconds)
                    ),

                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToList();

            // 3. Lấy thông tin Vehicle
            var vehicleIds = vehicles
                .Select(v => v.VehicleId)
                .ToList();

            var vehicleEntities = await _context.Vehicles
                .Where(v => vehicleIds.Contains(v.Id))
                .ToListAsync();

            // 4. Tạo response
            var result = vehicles.Select(v =>
            {
                var vehicleEntity = vehicleEntities
                    .FirstOrDefault(ve => ve.Id == v.VehicleId);

                return new
                {
                    VehicleId = v.VehicleId,
                    VehicleName = vehicleEntity?.Name,
                    Count = v.Count,
                    BestTime = v.BestTime,
                    AverageTime = v.AverageTime
                };
            });

            return Ok(result);
        }
    }
}
