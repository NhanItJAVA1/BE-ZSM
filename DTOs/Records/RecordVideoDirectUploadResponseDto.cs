namespace BE_ZSM.DTOs.Records
{
    public class RecordVideoDirectUploadResponseDto
    {
        public string ObjectKey { get; set; } = string.Empty;

        public string PublicUrl { get; set; } = string.Empty;

        public DateTime UploadedAtUtc { get; set; }
    }
}
