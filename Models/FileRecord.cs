namespace FileUploadApi.Models;

public class FileRecord
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    // Approval fields
    public string StageOfApproval { get; set; } = "Draft"; // Default stage
    public bool IsApproved { get; set; } = false;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime? ApprovalTime { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Foreign key to associate with a document
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
}
