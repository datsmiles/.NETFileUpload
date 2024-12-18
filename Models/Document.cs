namespace FileUploadApi.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Approval fields
        public string StageOfApproval { get; set; } = "Draft"; // Default stage is 'Draft'
        public bool IsApproved { get; set; } = false;
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime? ApprovalTime { get; set; } // Nullable because it may not be approved yet

        // Navigation property for associated files
        public ICollection<FileRecord> Files { get; set; } = new List<FileRecord>();
    }
}
