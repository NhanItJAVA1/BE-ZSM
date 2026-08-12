using BE_ZSM.DTOs.Catalog;
using BE_ZSM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogController : ControllerBase
    {
        private readonly S3PresignedUrlService _s3PresignedUrlService;

        public CatalogController(S3PresignedUrlService s3PresignedUrlService)
        {
            _s3PresignedUrlService = s3PresignedUrlService;
        }

        // POST: api/Catalog/image-upload-url
        [Authorize(Roles = "Admin")]
        [HttpPost("image-upload-url")]
        public ActionResult<CatalogImageUploadResponseDto> CreateImageUploadUrl(
            [FromBody] CreateCatalogImageUploadDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FileName))
            {
                return BadRequest(new
                {
                    message = "FileName is required"
                });
            }

            var contentType = string.IsNullOrWhiteSpace(dto.ContentType)
                ? "image/jpeg"
                : dto.ContentType;

            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "ContentType must be an image type"
                });
            }

            var result = _s3PresignedUrlService.CreateImageUploadUrl(
                dto.FileName,
                contentType,
                dto.Category);

            return Ok(new CatalogImageUploadResponseDto
            {
                UploadUrl = result.UploadUrl,
                ObjectKey = result.ObjectKey,
                PublicUrl = result.PublicUrl,
                ExpiresAtUtc = result.ExpiresAtUtc
            });
        }
    }
}
