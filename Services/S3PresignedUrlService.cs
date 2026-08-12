using Amazon.S3;
using Amazon.S3.Model;

namespace BE_ZSM.Services
{
    public sealed record PresignedUploadResult(
        string UploadUrl,
        string ObjectKey,
        string PublicUrl,
        DateTime ExpiresAtUtc);
        
    public sealed record DirectUploadResult(
        string ObjectKey,
        string PublicUrl,
        DateTime UploadedAtUtc);

    public class S3PresignedUrlService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _region;


        public S3PresignedUrlService(IAmazonS3 s3Client, IConfiguration configuration)
        {
            _s3Client = s3Client;
            _bucketName = configuration["AWS_BUCKET_NAME"]
                ?? throw new InvalidOperationException("AWS_BUCKET_NAME is missing.");
            _region = configuration["AWS_REGION"]
                ?? throw new InvalidOperationException("AWS_REGION is missing.");
        }

        public PresignedUploadResult CreateVideoUploadUrl(
            string fileName,
            string contentType,
            int expiresMinutes = 15)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".mp4";
            }

            var objectKey = $"records/videos/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = expiresAtUtc,
                ContentType = contentType
            };

            var uploadUrl = _s3Client.GetPreSignedURL(request);
            var publicUrl = $"https://{_bucketName}.s3.{_region}.amazonaws.com/{objectKey}";

            return new PresignedUploadResult(
                uploadUrl,
                objectKey,
                publicUrl,
                expiresAtUtc);
        }

        public PresignedUploadResult CreateImageUploadUrl(
            string fileName,
            string contentType,
            string category = "maps",
            int expiresMinutes = 15)
        {
            var safeCategory = category.Equals("vehicles", StringComparison.OrdinalIgnoreCase)
                ? "vehicles"
                : "maps";

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = contentType.StartsWith("image/png", StringComparison.OrdinalIgnoreCase)
                    ? ".png"
                    : contentType.StartsWith("image/webp", StringComparison.OrdinalIgnoreCase)
                        ? ".webp"
                        : ".jpg";
            }

            var objectKey =
                $"catalog/images/{safeCategory}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = expiresAtUtc,
                ContentType = contentType
            };

            var uploadUrl = _s3Client.GetPreSignedURL(request);
            var publicUrl = $"https://{_bucketName}.s3.{_region}.amazonaws.com/{objectKey}";

            return new PresignedUploadResult(
                uploadUrl,
                objectKey,
                publicUrl,
                expiresAtUtc);
        }

        public async Task<DirectUploadResult> UploadVideoAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".mp4";
            }

            var objectKey = $"records/videos/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
            var publicUrl = $"https://{_bucketName}.s3.{_region}.amazonaws.com/{objectKey}";

            await using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "video/mp4"
                    : file.ContentType
            };

            await _s3Client.PutObjectAsync(request);

            return new DirectUploadResult(
                objectKey,
                publicUrl,
                DateTime.UtcNow);
        }
        public string CreateGetUrl(
            string objectKey,
            int expiresMinutes = 15){
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expiresMinutes)
            };

            return _s3Client.GetPreSignedURL(request);
        }
        public string GetObjectKeyFromUrl(string url){
            var uri = new Uri(url);
            return uri.AbsolutePath.TrimStart('/');
        }
    }
}
