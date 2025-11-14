using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using StaffValidator.Core.Models;
using StaffValidator.Core.Repositories;
using StaffValidator.Core.Services;
using System.Text.Json;

namespace StaffValidator.WebApp.Controllers
{
    [Authorize] // Require authentication for all actions
    public partial class StaffController : Controller
    {
        private readonly IStaffRepository _repo;
        private readonly ValidatorService _validatorService;
        private readonly ILogger<StaffController> _logger;
        private readonly IWebHostEnvironment _env;

        public StaffController(IStaffRepository repo, ValidatorService validatorService, ILogger<StaffController> logger, IWebHostEnvironment env)
        {
            _repo = repo;
            _validatorService = validatorService;
            _logger = logger;
            _env = env;
        }

        public IActionResult Index()
        {
            _logger.LogInformation("📋 Displaying staff list page");
            var staffList = _repo.GetAll().OrderBy(s => s.StaffName);
            _logger.LogInformation("Found {StaffCount} staff members", staffList.Count());
            return View(staffList);
        }

        public IActionResult Create()
        {
            _logger.LogInformation("🆕 Displaying create staff form");
            return View(new Staff { StartingDate = DateTime.Now });
        }

        [HttpPost]
        public IActionResult Create(Staff model, IFormFile? photo)
        {
            _logger.LogInformation("➕ Attempting to create new staff: {StaffName} - {Email}",
                model.StaffName, model.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("❌ Model validation failed for staff creation: {StaffName}", model.StaffName);
                TempData["Error"] = "Please correct the validation errors and try again.";
                return View(model);
            }

            try
            {
                if (photo != null && photo.Length > 0)
                {
                    if (!TrySavePhoto(photo, out var relativePath, out var error))
                    {
                        ModelState.AddModelError("photo", error);
                        TempData["Error"] = error;
                        return View(model);
                    }
                    model.PhotoPath = relativePath;
                }

                _repo.Add(model);
                _logger.LogInformation("✅ Staff member created successfully: {StaffName} (ID: {StaffID})",
                    model.StaffName, model.StaffID);
                TempData["Success"] = $"Staff member '{model.StaffName}' has been successfully added!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error creating staff member: {StaffName}", model.StaffName);
                TempData["Error"] = "An error occurred while saving the staff member. Please try again.";
                return View(model);
            }
        }

        public IActionResult Details(int id)
        {
            _logger.LogInformation("👁️ Viewing staff details for ID: {StaffID}", id);
            var staff = _repo.Get(id);
            if (staff == null)
            {
                _logger.LogWarning("⚠️ Staff not found with ID: {StaffID}", id);
                TempData["Error"] = $"Staff member with ID {id} was not found.";
                return RedirectToAction("Index");
            }
            _logger.LogInformation("✅ Staff details loaded: {StaffName}", staff.StaffName);
            return View(staff);
        }

        public IActionResult Edit(int id)
        {
            _logger.LogInformation("✏️ Loading edit form for staff ID: {StaffID}", id);
            var staff = _repo.Get(id);
            if (staff == null)
            {
                _logger.LogWarning("⚠️ Staff not found for editing with ID: {StaffID}", id);
                TempData["Error"] = $"Staff member with ID {id} was not found.";
                return RedirectToAction("Index");
            }
            _logger.LogInformation("✅ Edit form loaded for staff: {StaffName}", staff.StaffName);
            return View(staff);
        }

        [HttpPost]
        public IActionResult Edit(Staff model, IFormFile? photo, bool removeExistingPhoto = false)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the validation errors and try again.";
                return View(model);
            }

            try
            {
                var existingStaff = _repo.Get(model.StaffID);
                if (existingStaff == null)
                {
                    TempData["Error"] = "Staff member not found.";
                    return RedirectToAction("Index");
                }

                // Remove existing photo if requested
                if (removeExistingPhoto && !string.IsNullOrEmpty(existingStaff.PhotoPath))
                {
                    TryDeletePhoto(existingStaff.PhotoPath);
                    existingStaff.PhotoPath = string.Empty;
                }

                // Handle photo upload
                if (photo != null && photo.Length > 0)
                {
                    if (!TrySavePhoto(photo, out var relativePath, out var error))
                    {
                        ModelState.AddModelError("photo", error);
                        TempData["Error"] = error;
                        return View(model);
                    }

                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(existingStaff.PhotoPath))
                    {
                        TryDeletePhoto(existingStaff.PhotoPath);
                    }

                    model.PhotoPath = relativePath;
                }
                else
                {
                    // Keep existing photo if no new photo uploaded
                    model.PhotoPath = existingStaff.PhotoPath;
                }

                _repo.Update(model);
                TempData["Success"] = $"Staff member '{model.StaffName}' has been successfully updated!";
                return RedirectToAction("Details", new { id = model.StaffID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error updating staff member: {StaffName} (ID: {StaffID})",
                    model.StaffName, model.StaffID);
                TempData["Error"] = "An error occurred while updating the staff member. Please try again.";
                return View(model);
            }
        }

        [HttpPost]
        [Authorize(Policy = "Manager")] // Only managers and admins can delete
        public IActionResult Delete(int id)
        {
            _logger.LogInformation("🗑️ Attempting to delete staff with ID: {StaffID}", id);

            try
            {
                var staff = _repo.Get(id);
                if (staff == null)
                {
                    _logger.LogWarning("⚠️ Staff not found for deletion with ID: {StaffID}", id);
                    TempData["Error"] = $"Staff member with ID {id} was not found.";
                    return RedirectToAction("Index");
                }

                // Delete photo if exists
                if (!string.IsNullOrEmpty(staff.PhotoPath))
                {
                    TryDeletePhoto(staff.PhotoPath);
                }

                _repo.Delete(id);
                _logger.LogInformation("✅ Staff member deleted successfully: {StaffName} (ID: {StaffID})",
                    staff.StaffName, staff.StaffID);
                TempData["Success"] = $"Staff member '{staff.StaffName}' has been successfully deleted!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error deleting staff member with ID: {StaffID}", id);
                TempData["Error"] = "An error occurred while deleting the staff member. Please try again.";
                return RedirectToAction("Index");
            }
        }

        public IActionResult Upload()
        {
            _logger.LogInformation("📤 Displaying JSON upload form");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile? jsonFile)
        {
            _logger.LogInformation("📤 Processing JSON file upload");

            if (jsonFile == null || jsonFile.Length == 0)
            {
                _logger.LogWarning("❌ No file selected for upload");
                TempData["Error"] = "Please select a JSON file to upload.";
                return View();
            }

            // Validate file type
            if (!jsonFile.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("🚫 Invalid file type attempted: {FileName}", jsonFile.FileName);
                TempData["Error"] = "Only JSON files are allowed.";
                return View();
            }

            // Validate file size (max 10MB)
            if (jsonFile.Length > 10 * 1024 * 1024)
            {
                _logger.LogWarning("📏 File too large: {Size} bytes", jsonFile.Length);
                TempData["Error"] = "File size cannot exceed 10MB.";
                return View();
            }

            try
            {
                using var streamReader = new StreamReader(jsonFile.OpenReadStream());
                var jsonContent = await streamReader.ReadToEndAsync();

                _logger.LogInformation("📋 Parsing JSON content, size: {Size} characters", jsonContent.Length);

                // Parse JSON to Staff array
                var staffMembers = JsonSerializer.Deserialize<Staff[]>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (staffMembers == null || staffMembers.Length == 0)
                {
                    _logger.LogWarning("❌ No valid staff data found in JSON file");
                    TempData["Error"] = "No valid staff data found in the JSON file.";
                    return View();
                }

                var successCount = 0;
                var errorCount = 0;
                var validationErrors = new List<string>();

                foreach (var staff in staffMembers)
                {
                    try
                    {
                        // Validate using ValidatorService (Hybrid)
                        var (isValid, errors) = _validatorService.ValidateAll(staff);

                        if (isValid)
                        {
                            // Check if staff already exists (by email)
                            var existingStaff = _repo.GetAll().FirstOrDefault(s => s.Email.Equals(staff.Email, StringComparison.OrdinalIgnoreCase));

                            if (existingStaff != null)
                            {
                                _logger.LogInformation("🔄 Updating existing staff: {Email}", staff.Email);
                                staff.StaffID = existingStaff.StaffID;
                                staff.PhotoPath = existingStaff.PhotoPath; // Keep existing photo
                                _repo.Update(staff);
                            }
                            else
                            {
                                _logger.LogInformation("➕ Adding new staff: {Email}", staff.Email);
                                _repo.Add(staff);
                            }
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            var errorMsg = $"Staff '{staff.StaffName}' ({staff.Email}): {string.Join(", ", errors)}";
                            validationErrors.Add(errorMsg);
                            _logger.LogWarning("❌ Validation failed for staff: {Email} - {Errors}", staff.Email, errorMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        var errorMsg = $"Staff '{staff.StaffName}' ({staff.Email}): {ex.Message}";
                        validationErrors.Add(errorMsg);
                        _logger.LogError(ex, "💥 Error processing staff: {Email}", staff.Email);
                    }
                }

                // Prepare result message
                var resultMessage = $"Upload completed! Successfully processed: {successCount}, Failed: {errorCount}";

                if (successCount > 0)
                {
                    TempData["Success"] = resultMessage;
                    _logger.LogInformation("✅ JSON upload completed: {SuccessCount} success, {ErrorCount} errors",
                        successCount, errorCount);
                }

                if (errorCount > 0)
                {
                    TempData["ValidationErrors"] = validationErrors;
                    if (successCount == 0)
                    {
                        TempData["Error"] = resultMessage;
                    }
                }

                return RedirectToAction("Index");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "💥 JSON parsing error");
                TempData["Error"] = "Invalid JSON format. Please check your file and try again.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error during JSON upload");
                TempData["Error"] = "An unexpected error occurred while processing the file. Please try again.";
                return View();
            }
        }
    }
}

namespace StaffValidator.WebApp.Controllers
{
    public partial class StaffController
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        private string GetWebRoot()
        {
            return _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        private string? TryGetPhysicalPhotoPath(string? relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) return null;
            var trimmed = relative.TrimStart('/', '\\');
            var normalized = trimmed.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(GetWebRoot(), normalized);
        }

        private void TryDeletePhoto(string? relative)
        {
            try
            {
                var physical = TryGetPhysicalPhotoPath(relative);
                if (physical != null && System.IO.File.Exists(physical))
                {
                    System.IO.File.Delete(physical);
                    _logger.LogInformation("🗑️ Photo deleted: {PhotoPath}", relative);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete photo: {PhotoPath}", relative);
            }
        }

        private bool TrySavePhoto(IFormFile file, out string relativePath, out string error)
        {
            relativePath = string.Empty;
            error = string.Empty;

            try
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                {
                    error = "Only JPG, JPEG, PNG, GIF or WEBP files are allowed.";
                    return false;
                }

                if (file.Length > 5 * 1024 * 1024)
                {
                    error = "File size cannot exceed 5MB.";
                    return false;
                }

                var webRoot = _env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                {
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var uploadsDir = Path.Combine(webRoot, "uploads");
                Directory.CreateDirectory(uploadsDir);

                var safeName = Path.GetFileName(file.FileName);
                var fname = $"{Guid.NewGuid()}_{safeName}";
                var physicalPath = Path.Combine(uploadsDir, fname);
                using (var fs = new FileStream(physicalPath, FileMode.Create))
                {
                    file.CopyTo(fs);
                }

                relativePath = $"/uploads/{fname}";
                _logger.LogInformation("✅ Photo uploaded: {File} -> {Path}", safeName, relativePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo upload failed: {Name}", file?.FileName);
                error = "Failed to upload photo. Please try again.";
                return false;
            }
        }
    }
}
