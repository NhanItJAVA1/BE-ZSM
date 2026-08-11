namespace BE_ZSM.DTOs.Records
{
    public class RecordVideoUploadResponseDto
    {
        public string UploadUrl { get; set; } = string.Empty;

        public string ObjectKey { get; set; } = string.Empty;

        public string PublicUrl { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }
    }
}
