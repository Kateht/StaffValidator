using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using StaffValidator.Core.Models;
using StaffValidator.Core.Repositories;
using StaffValidator.Core.Services;
using System.Text.Json;

namespace StaffValidator.WebApp.Controllers
{
    [Authorize] // Require authentication for all actions
    public class StaffController : Controller
    {
        private readonly IStaffRepository _repo;
        private readonly ValidatorService _validatorService;
        private readonly ILogger<StaffController> _logger;

        public StaffController(IStaffRepository repo, ValidatorService validatorService, ILogger<StaffController> logger)
        {
            _repo = repo;
            _validatorService = validatorService;
            _logger = logger;
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
                    _logger.LogInformation("📸 Processing photo upload: {FileName} ({Size} bytes)", 
                        photo.FileName, photo.Length);

                    // Validate file type and size
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                    
                    if (!allowedExtensions.Contains(extension))
                    {
                        _logger.LogWarning("🚫 Invalid file type attempted: {Extension} for staff: {StaffName}", 
                            extension, model.StaffName);
                        ModelState.AddModelError("photo", "Only JPG, PNG, and GIF files are allowed.");
                        TempData["Error"] = "Invalid file type. Please upload a valid image file.";
                        return View(model);
                    }

                    if (photo.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        _logger.LogWarning("📏 File too large: {Size} bytes for staff: {StaffName}", 
                            photo.Length, model.StaffName);
                        ModelState.AddModelError("photo", "File size cannot exceed 5MB.");
                        TempData["Error"] = "File is too large. Please upload an image smaller than 5MB.";
                        return View(model);
                    }

                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    Directory.CreateDirectory(path);
                    var fname = $"{Guid.NewGuid()}_{Path.GetFileName(photo.FileName)}";
                    using var fs = new FileStream(Path.Combine(path, fname), FileMode.Create);
                    photo.CopyTo(fs);
                    model.PhotoPath = $"/uploads/{fname}";
                    
                    _logger.LogInformation("✅ Photo uploaded successfully: {PhotoPath}", model.PhotoPath);
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
        public IActionResult Edit(Staff model, IFormFile? photo)
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

                // Handle photo upload
                if (photo != null && photo.Length > 0)
                {
                    // Validate file type and size (same as Create)
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                    
                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("photo", "Only JPG, PNG, and GIF files are allowed.");
                        TempData["Error"] = "Invalid file type. Please upload a valid image file.";
                        return View(model);
                    }

                    if (photo.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        ModelState.AddModelError("photo", "File size cannot exceed 5MB.");
                        TempData["Error"] = "File is too large. Please upload an image smaller than 5MB.";
                        return View(model);
                    }

                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(existingStaff.PhotoPath))
                    {
                        var oldPhotoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingStaff.PhotoPath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPhotoPath))
                        {
                            System.IO.File.Delete(oldPhotoPath);
                        }
                    }

                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    Directory.CreateDirectory(path);
                    var fname = $"{Guid.NewGuid()}_{Path.GetFileName(photo.FileName)}";
                    using var fs = new FileStream(Path.Combine(path, fname), FileMode.Create);
                    photo.CopyTo(fs);
                    model.PhotoPath = $"/uploads/{fname}";
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
                    var photoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", staff.PhotoPath.TrimStart('/'));
                    if (System.IO.File.Exists(photoPath))
                    {
                        System.IO.File.Delete(photoPath);
                        _logger.LogInformation("🗑️ Photo deleted: {PhotoPath}", staff.PhotoPath);
                    }
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
                        // Validate using ValidatorService
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
