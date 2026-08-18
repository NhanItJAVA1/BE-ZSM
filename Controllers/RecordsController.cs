using BE_ZSM.DTOs.Records;
using BE_ZSM.Entities;
using BE_ZSM.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

[ApiController]
[Route("api/[controller]")]
public class RecordsController : ControllerBase
{
    private readonly IRecordService _recordService;
    private static readonly Counter VideoUploadCounter = Metrics
        .CreateCounter("zsm_video_uploads_total", "Tổng số lượt tạo URL upload video S3");

    public RecordsController(IRecordService recordService)
    {
        _recordService = recordService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecords()
    {
        var records =
            await _recordService.GetRecordsAsync();

        return Ok(records);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRecord(int id)
    {
        var record =
            await _recordService.GetRecordAsync(id);

        return Ok(record);
    }

    [HttpGet("records-by-user/{userId}")]
    public async Task<IActionResult> GetRecordsByUser(int userId)
    {
        var records =
            await _recordService.GetRecordsByUserAsync(userId);

        return Ok(records);
    }

    [Authorize]
    [HttpGet("admin/records/pending")]
    public async Task<IActionResult> GetPendingRecords()
    {
        var records =
            await _recordService.GetPendingRecordsAsync(User);

        return Ok(records);
    }

    [Authorize]
    [HttpPut("admin/records/{id}/approve")]
    public async Task<IActionResult> ApproveRecord(int id)
    {
        await _recordService.ApproveRecordAsync(id, User);

        return NoContent();
    }

    [Authorize]
    [HttpPut("admin/records/{id}/reject")]
    public async Task<IActionResult> RejectRecord(int id,[FromQuery] string? reason = null)
    {
        await _recordService.RejectRecordAsync(
            id,
            reason,
            User);

        return NoContent();
    }

    [Authorize]
    [HttpPost("video-upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<RecordVideoDirectUploadResponseDto>>UploadVideo([FromForm] VideoUploadFormDto form)
    {
        var result =
            await _recordService.UploadVideoAsync(form);

        // Increment the Prometheus counter for video uploads
        VideoUploadCounter.Inc();

        return Ok(result);
    }

    [Authorize]
    [HttpPost("video-upload-url")]
    public ActionResult<RecordVideoUploadResponseDto> CreateVideoUploadUrl([FromBody] CreateRecordVideoUploadDto dto)
    {
        var result =
            _recordService.CreateVideoUploadUrl(dto);

        return Ok(result);
    }

    [HttpGet("/recommendations/maps/{mapId}/vehicles")]
    public async Task<IActionResult> GetRecommendationVehicles(int mapId)
    {
        var result =
            await _recordService
                .GetRecommendationVehiclesAsync(mapId);

        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateRecord([FromBody] CreateRecordDto dto)
    {
        var record = await _recordService.CreateRecordAsync(dto);

        return CreatedAtAction(
            nameof(GetRecord),
            new { id = record.Id },
            record);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRecord(int id,[FromBody] CreateRecordDto dto)
    {
        var record =
            await _recordService.UpdateRecordAsync(id, dto);

        return Ok(record);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRecord(int id)
    {
        await _recordService.DeleteRecordAsync(id);

        return NoContent();
    }
}