using FileUploadApi.Data;
using FileUploadApi.Models;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace FileUploadApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileUploadController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public FileUploadController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    // Endpoint to create a document and upload associated files
    [HttpPost("create")]
    public async Task<IActionResult> CreateDocumentWithFiles([FromForm] string title, [FromForm] List<IFormFile> files)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest("Document title is required.");
        }

        if (files == null || !files.Any())
        {
            return BadRequest("At least one file must be uploaded.");
        }

        var document = new Document
        {
            Title = title,
            StageOfApproval = "Initial", // Starting stage
            CreatedAt = DateTime.UtcNow,
            IsApproved = false,
            ApprovedBy = string.Empty,
            ApprovalTime = null
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var uploadsFolder = Path.Combine(_env.ContentRootPath, "UploadedFiles");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileRecords = new List<FileRecord>();

        foreach (var file in files)
        {
            if (file.Length > 0)
            {
                var filePath = Path.Combine(uploadsFolder, file.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                fileRecords.Add(new FileRecord
                {
                    FileName = file.FileName,
                    FilePath = filePath,
                    DocumentId = document.Id,
                    StageOfApproval = "Initial", // Starting stage for file
                    IsApproved = false,
                    ApprovedBy = string.Empty,
                    ApprovalTime = null,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }
        }

        _context.FileRecords.AddRange(fileRecords);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            DocumentId = document.Id,
            document.Title,
            document.StageOfApproval,
            Files = fileRecords.Select(fr => new
            {
                fr.Id,
                fr.FileName,
                fr.FilePath,
                fr.StageOfApproval,
                fr.IsApproved
            })
        });
    }

    // Change the stage of approval for a file or document (Progress it)
    [HttpPost("advance-stage/{id}")]
    public async Task<IActionResult> AdvanceStage(int id, [FromForm] string currentStage)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
        {
            return NotFound($"Document with ID {id} not found.");
        }

        // Determine next stage (this logic can be adjusted as per your approval stages)
        var nextStage = currentStage switch
        {
            "Initial" => "Manager Approval",
            "Manager Approval" => "Director Approval",
            "Director Approval" => "Final Approval", // Final stage where the document can be approved
            _ => currentStage // If it's already at final stage, keep it as is
        };

        document.StageOfApproval = nextStage;
        await _context.SaveChangesAsync();

        // Also update the files' stage (if necessary)
        var filesInDocument = await _context.FileRecords.Where(fr => fr.DocumentId == document.Id && !fr.IsDeleted).ToListAsync();
        foreach (var file in filesInDocument)
        {
            file.StageOfApproval = nextStage;
        }
        await _context.SaveChangesAsync();

        return Ok(new
        {
            document.Id,
            document.Title,
            document.StageOfApproval,
            Files = filesInDocument.Select(fr => new
            {
                fr.Id,
                fr.FileName,
                fr.StageOfApproval
            })
        });
    }

    // Approve a document (final approval stage)
    [HttpPost("approve/{documentId}")]
    public async Task<IActionResult> ApproveDocument(int documentId, [FromForm] string approvedBy)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            return NotFound($"Document with ID {documentId} not found.");
        }

        if (document.StageOfApproval != "Final Approval")
        {
            return BadRequest("Document cannot be approved unless it is at the final approval stage.");
        }

        document.IsApproved = true;
        document.ApprovedBy = approvedBy;
        document.ApprovalTime = DateTime.UtcNow;

        var fileRecords = await _context.FileRecords
            .Where(fr => fr.DocumentId == documentId && !fr.IsDeleted)
            .ToListAsync();

        foreach (var file in fileRecords)
        {
            if (file.StageOfApproval == "Final Approval") // Only files at final approval can be approved
            {
                file.IsApproved = true;
                file.ApprovedBy = approvedBy;
                file.ApprovalTime = document.ApprovalTime;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            document.Id,
            document.Title,
            document.IsApproved,
            document.ApprovedBy,
            document.ApprovalTime
        });
    }

    // Reject a document (final rejection stage)
    [HttpPost("reject/{documentId}")]
    public async Task<IActionResult> RejectDocument(int documentId, [FromForm] string rejectedBy)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            return NotFound($"Document with ID {documentId} not found.");
        }

        if (document.StageOfApproval != "Final Approval")
        {
            return BadRequest("Document cannot be rejected unless it is at the final approval stage.");
        }

        document.IsApproved = false;
        document.ApprovedBy = rejectedBy;
        document.ApprovalTime = DateTime.UtcNow;

        var fileRecords = await _context.FileRecords
            .Where(fr => fr.DocumentId == documentId && !fr.IsDeleted)
            .ToListAsync();

        foreach (var file in fileRecords)
        {
            if (file.StageOfApproval == "Final Approval") // Only files at final approval can be rejected
            {
                file.IsApproved = false;
                file.ApprovedBy = rejectedBy;
                file.ApprovalTime = document.ApprovalTime;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            document.Id,
            document.Title,
            document.IsApproved,
            document.ApprovedBy,
            document.ApprovalTime
        });
    }

    // Approve a single file by file ID (file-specific approval)
    [HttpPost("file/approve/{fileId}")]
    public async Task<IActionResult> ApproveFile(int fileId, [FromForm] string approvedBy)
    {
        var fileRecord = await _context.FileRecords.FindAsync(fileId);
        if (fileRecord == null || fileRecord.IsDeleted)
        {
            return NotFound($"File with ID {fileId} not found.");
        }

        // Ensure the file is at the correct stage for approval
        if (fileRecord.StageOfApproval != "Final Approval")
        {
            return BadRequest("File cannot be approved unless it is at the final approval stage.");
        }

        fileRecord.IsApproved = true;
        fileRecord.ApprovedBy = approvedBy;
        fileRecord.ApprovalTime = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            fileRecord.Id,
            fileRecord.FileName,
            fileRecord.IsApproved,
            fileRecord.ApprovedBy,
            fileRecord.ApprovalTime
        });
    }

    // Reject a single file by file ID (file-specific rejection)
    [HttpPost("file/reject/{fileId}")]
    public async Task<IActionResult> RejectFile(int fileId, [FromForm] string rejectedBy)
    {
        var fileRecord = await _context.FileRecords.FindAsync(fileId);
        if (fileRecord == null || fileRecord.IsDeleted)
        {
            return NotFound($"File with ID {fileId} not found.");
        }

        // Ensure the file is at the correct stage for rejection
        if (fileRecord.StageOfApproval != "Final Approval")
        {
            return BadRequest("File cannot be rejected unless it is at the final approval stage.");
        }

        fileRecord.IsApproved = false;
        fileRecord.ApprovedBy = rejectedBy;
        fileRecord.ApprovalTime = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            fileRecord.Id,
            fileRecord.FileName,
            fileRecord.IsApproved,
            fileRecord.ApprovedBy,
            fileRecord.ApprovalTime
        });
    }

    // Re-upload a file after rejection or other reason
    [HttpPost("file/reupload/{fileId}")]
    public async Task<IActionResult> ReuploadFile(int fileId, [FromForm] IFormFile file)
    {
        var fileRecord = await _context.FileRecords.FindAsync(fileId);
        if (fileRecord == null)
        {
            return NotFound($"File with ID {fileId} not found.");
        }

        // Delete the old file
        if (System.IO.File.Exists(fileRecord.FilePath))
        {
            System.IO.File.Delete(fileRecord.FilePath);
        }

        var uploadsFolder = Path.Combine(_env.ContentRootPath, "UploadedFiles");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var newFilePath = Path.Combine(uploadsFolder, file.FileName);

        using (var stream = new FileStream(newFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        fileRecord.FilePath = newFilePath;
        fileRecord.FileName = file.FileName;
        fileRecord.StageOfApproval = "Initial"; // Reset to initial stage
        fileRecord.IsApproved = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            fileRecord.Id,
            fileRecord.FileName,
            fileRecord.FilePath,
            fileRecord.StageOfApproval,
            fileRecord.IsApproved
        });


    }

    // Get a single file record in JSON format
    [HttpGet("file/{id}")]
    public async Task<IActionResult> GetSingleFile(int id)
    {
        var fileRecord = await _context.FileRecords.FindAsync(id);
        if (fileRecord == null || fileRecord.IsDeleted)
        {
            return NotFound($"No file found with ID {id}.");
        }

        return Ok(fileRecord);
    }

    // Download a single file by its ID
    [HttpGet("file/download/{id}")]
    public async Task<IActionResult> DownloadFile(int id)
    {
        var fileRecord = await _context.FileRecords.FindAsync(id);
        if (fileRecord == null || fileRecord.IsDeleted)
        {
            return NotFound($"No file found with ID {id}.");
        }

        if (!System.IO.File.Exists(fileRecord.FilePath))
        {
            return NotFound("File not found on server.");
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(fileRecord.FilePath);
        return File(fileBytes, "application/octet-stream", fileRecord.FileName);
    }

    // Preview a single file by its ID
    [HttpGet("file/preview/{id}")]
    public async Task<IActionResult> PreviewFile(int id)
    {
        var fileRecord = await _context.FileRecords.FindAsync(id);
        if (fileRecord == null || fileRecord.IsDeleted)
        {
            return NotFound($"No file found with ID {id}.");
        }

        if (!System.IO.File.Exists(fileRecord.FilePath))
        {
            return NotFound("File not found on server.");
        }

        var contentType = GetContentType(fileRecord.FilePath);
        var fileBytes = await System.IO.File.ReadAllBytesAsync(fileRecord.FilePath);
        return File(fileBytes, contentType);
    }

    // Get all files for a document in JSON format
    [HttpGet("document/{documentId}/files")]
    public async Task<IActionResult> GetFilesByDocument(int documentId)
    {
        var files = await _context.FileRecords
            .Where(fr => fr.DocumentId == documentId && !fr.IsDeleted)
            .ToListAsync();

        if (!files.Any())
        {
            return NotFound($"No files found for document ID {documentId}.");
        }

        return Ok(files);
    }

    // Download all files for a document as a zip
    [HttpGet("document/{documentId}/download")]
    public async Task<IActionResult> DownloadFilesForDocument(int documentId)
    {
        var files = await _context.FileRecords
            .Where(fr => fr.DocumentId == documentId && !fr.IsDeleted)
            .ToListAsync();

        if (!files.Any())
        {
            return NotFound($"No files found for document ID {documentId}.");
        }

        var zipFileName = $"Document_{documentId}_Files.zip";
        var zipPath = Path.Combine(_env.ContentRootPath, zipFileName);

        using (var archive = new FileStream(zipPath, FileMode.Create))
        using (var zip = new System.IO.Compression.ZipArchive(archive, System.IO.Compression.ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                if (System.IO.File.Exists(file.FilePath))
                {
                    zip.CreateEntryFromFile(file.FilePath, Path.GetFileName(file.FilePath));
                }
            }
        }

        var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
        System.IO.File.Delete(zipPath); // Clean up temporary zip file
        return File(zipBytes, "application/zip", zipFileName);
    }

    [HttpGet("document/{documentId}/preview")]
    public async Task<IActionResult> PreviewFilesForDocument(int documentId)
    {
        // Fetch all files associated with the document and are not deleted
        var files = await _context.FileRecords
            .Where(fr => fr.DocumentId == documentId && !fr.IsDeleted)
            .ToListAsync();

        if (!files.Any())
        {
            return NotFound($"No files found for document ID {documentId}.");
        }

        var previews = new List<object>();

        foreach (var fileRecord in files)
        {
            if (!System.IO.File.Exists(fileRecord.FilePath))
            {
                previews.Add(new
                {
                    FileName = fileRecord.FileName,
                    Message = "File not found on server"
                });
                continue;
            }

            // Get the content type of the file
            var contentType = GetContentType(fileRecord.FilePath);

            // Read the file content
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fileRecord.FilePath);

            // Base64 encode the file content for safe transport in JSON
            var fileContentBase64 = Convert.ToBase64String(fileBytes);

            previews.Add(new
            {
                FileName = fileRecord.FileName,
                ContentType = contentType,
                FileContentBase64 = fileContentBase64
            });
        }

        return Ok(new
        {
            Message = "Files preview generated successfully",
            Previews = previews
        });
    }

    [HttpGet("document/{documentId}/preview2")]
    [DisableRequestTimeout]
    public async Task<IActionResult> PreviewFilesForDocument2(int documentId)
    {
        // Fetch all files associated with the document and are not deleted
        var files = await _context.FileRecords
            .Where(fr => fr.DocumentId == documentId && !fr.IsDeleted)
            .ToListAsync();

        if (!files.Any())
        {
            return NotFound($"No files found for document ID {documentId}.");
        }

        // Build an HTML page dynamically with embedded previews
        var htmlBuilder = new System.Text.StringBuilder();
        htmlBuilder.Append("<!DOCTYPE html><html><head><title>Document Preview</title></head><body>");
        htmlBuilder.Append("<h1>Document Preview</h1>");

        foreach (var fileRecord in files)
        {
            if (!System.IO.File.Exists(fileRecord.FilePath))
            {
                htmlBuilder.Append($"<p>File <strong>{fileRecord.FileName}</strong> not found on server.</p>");
                continue;
            }

            var contentType = GetContentType(fileRecord.FilePath);

            // Embed files based on their type
            if (contentType.StartsWith("image/"))
            {
                htmlBuilder.Append($"<h3>{fileRecord.FileName}</h3>");
                htmlBuilder.Append($"<img src=\"/api/FileUpload/file/preview/{fileRecord.Id}\" alt=\"{fileRecord.FileName}\" style=\"max-width:100%; height:auto;\" />");
            }
            else if (contentType == "application/pdf")
            {
                htmlBuilder.Append($"<h3>{fileRecord.FileName}</h3>");
                htmlBuilder.Append($"<iframe src=\"/api/FileUpload/file/preview/{fileRecord.Id}\" style=\"width:100%; height:600px; border:none;\"></iframe>");
            }
            else
            {
                htmlBuilder.Append($"<p>File <strong>{fileRecord.FileName}</strong> cannot be previewed inline. <a href=\"/api/FileUpload/file/preview/{fileRecord.Id}\" target=\"_blank\">Download</a></p>");
            }
        }

        htmlBuilder.Append("</body></html>");

        return Content(htmlBuilder.ToString(), "text/html");
    }

    // Helper method to determine MIME type
    private string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".html" => "text/html",
            _ => "application/octet-stream",
        };
    }
}
