namespace BE_ZSM.DTOs.Catalog
{
    public class CreateCatalogImageUploadDto
    {
        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string Category { get; set; } = "maps";
    }
}
