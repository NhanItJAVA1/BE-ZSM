using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public RecordsController(
            AppDbContext context,
            RecordHelper recordHelper,
            DbSaveHelper dbSaveHelper,
            S3PresignedUrlService s3PresignedUrlService,
            RecordMapperHelper recordMapperHelper)
        {
            _context = context;
            _recordHelper = recordHelper;
            _dbSaveHelper = dbSaveHelper;
            _s3PresignedUrlService = s3PresignedUrlService;
            _recordMapperHelper = recordMapperHelper;
        }

        // GET: api/Records
        [HttpGet]
        public async Task<IActionResult> GetRecords()
        {
            try
            {
                var records = await _context.Records
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

        // TEST ENDPOINT - Remove after debugging
        [HttpGet("test")]
        public IActionResult TestEndpoint()
        {
            return Ok(new { message = "Controller is working", timestamp = DateTime.UtcNow });
        }

        // GET: api/Records/{id}
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

        // POST: api/Records/video-upload
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

        // POST: api/Records/video-upload-url
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

        // POST: api/Records
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

            // Reload with related entities to return DTO
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

        // PUT: api/Records/{id}
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

        // DELETE: api/Records/{id}
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
    }
}
