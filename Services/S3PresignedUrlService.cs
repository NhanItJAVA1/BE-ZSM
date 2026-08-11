using Amazon.S3;
using Amazon.S3.Model;

namespace BE_ZSM.Services
{
    public sealed record PresignedUploadResult(
        string UploadUrl,
        string ObjectKey,
        string PublicUrl,
        DateTime ExpiresAtUtc);

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
    }
}
