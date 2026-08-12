namespace BE_ZSM.DTOs.Records
{
    /// <summary>
    /// Form model for direct video file upload to S3.
    /// Swashbuckle requires this wrapper to properly generate multipart/form-data schema.
    /// </summary>
    public class VideoUploadFormDto
    {
        /// <summary>
        /// The video file to upload.
        /// </summary>
        public IFormFile? VideoFile { get; set; }
    }
}
