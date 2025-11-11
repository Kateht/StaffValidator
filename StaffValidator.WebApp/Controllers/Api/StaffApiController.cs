using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffValidator.Core.Models;
using StaffValidator.Core.Repositories;
using StaffValidator.Core.Services;
using System.ComponentModel.DataAnnotations;

namespace StaffValidator.WebApp.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class StaffApiController : ControllerBase
{
    private readonly IStaffRepository _repository;
    private readonly ValidatorService _validatorService;
    private readonly ILogger<StaffApiController> _logger;

    public StaffApiController(IStaffRepository repository, ValidatorService validatorService, ILogger<StaffApiController> logger)
    {
        _repository = repository;
        _validatorService = validatorService;
        _logger = logger;
    }

    /// <summary>
    /// Get all staff members
    /// </summary>
    /// <returns>List of all staff members</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetAllStaff()
    {
        _logger.LogInformation("🔍 API: Getting all staff members");
        
        try
        {
            var staff = _repository.GetAll();
            var staffDto = staff.Select(s => new StaffDto
            {
                StaffID = s.StaffID,
                StaffName = s.StaffName,
                Email = s.Email,
                PhoneNumber = s.PhoneNumber,
                StartingDate = s.StartingDate,
                PhotoPath = s.PhotoPath
            });

            _logger.LogInformation("✅ API: Retrieved {Count} staff members", staff.Count());
            return Ok(new ApiResponse<IEnumerable<StaffDto>>(true, "Staff retrieved successfully", staffDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 API: Error retrieving staff members");
            return StatusCode(500, new ApiResponse<object>(false, "Internal server error occurred"));
        }
    }

    /// <summary>
    /// Get a specific staff member by ID
    /// </summary>
    /// <param name="id">Staff member ID</param>
    /// <returns>Staff member details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StaffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetStaff(int id)
    {
        _logger.LogInformation("🔍 API: Getting staff member with ID: {StaffID}", id);
        
        try
        {
            var staff = _repository.Get(id);
            if (staff == null)
            {
                _logger.LogWarning("⚠️ API: Staff not found with ID: {StaffID}", id);
                return NotFound(new ApiResponse<object>(false, $"Staff member with ID {id} not found"));
            }

            var staffDto = new StaffDto
            {
                StaffID = staff.StaffID,
                StaffName = staff.StaffName,
                Email = staff.Email,
                PhoneNumber = staff.PhoneNumber,
                StartingDate = staff.StartingDate,
                PhotoPath = staff.PhotoPath
            };

            _logger.LogInformation("✅ API: Retrieved staff: {StaffName}", staff.StaffName);
            return Ok(new ApiResponse<StaffDto>(true, "Staff retrieved successfully", staffDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 API: Error retrieving staff with ID: {StaffID}", id);
            return StatusCode(500, new ApiResponse<object>(false, "Internal server error occurred"));
        }
    }

    /// <summary>
    /// Create a new staff member
    /// </summary>
    /// <param name="createStaffDto">Staff creation data</param>
    /// <returns>Created staff member</returns>
    [HttpPost]
    [ProducesResponseType(typeof(StaffDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult CreateStaff([FromBody] CreateStaffDto createStaffDto)
    {
        _logger.LogInformation("➕ API: Creating new staff: {StaffName}", createStaffDto.StaffName);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("❌ API: Invalid model state for staff creation");
            return BadRequest(new ApiResponse<object>(false, "Validation failed", ModelState));
        }

        // Additional project-wide validation (email & phone) using the hybrid validator
        var staff = new Staff
        {
            StaffName = createStaffDto.StaffName,
            Email = createStaffDto.Email,
            PhoneNumber = createStaffDto.PhoneNumber ?? string.Empty,
            StartingDate = createStaffDto.StartingDate
        };

        (bool isValidModel, System.Collections.Generic.List<string> modelErrors) = _validatorService.ValidateAll(staff);
        if (!isValidModel)
        {
            _logger.LogWarning("❌ API: Hybrid validation failed for staff creation: {Errors}", string.Join("; ", modelErrors));
            return BadRequest(new ApiResponse<object>(false, "Validation failed", modelErrors));
        }

        try
        {
            _repository.Add(staff);

            var staffDto = new StaffDto
            {
                StaffID = staff.StaffID,
                StaffName = staff.StaffName,
                Email = staff.Email,
                PhoneNumber = staff.PhoneNumber,
                StartingDate = staff.StartingDate,
                PhotoPath = staff.PhotoPath
            };

            _logger.LogInformation("✅ API: Staff created successfully: {StaffName} (ID: {StaffID})", 
                staff.StaffName, staff.StaffID);

            return CreatedAtAction(nameof(GetStaff), new { id = staff.StaffID }, 
                new ApiResponse<StaffDto>(true, "Staff created successfully", staffDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 API: Error creating staff: {StaffName}", createStaffDto.StaffName);
            return StatusCode(500, new ApiResponse<object>(false, "Internal server error occurred"));
        }
    }

    /// <summary>
    /// Update an existing staff member
    /// </summary>
    /// <param name="id">Staff member ID</param>
    /// <param name="updateStaffDto">Staff update data</param>
    /// <returns>Updated staff member</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(StaffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult UpdateStaff(int id, [FromBody] UpdateStaffDto updateStaffDto)
    {
        _logger.LogInformation("✏️ API: Updating staff with ID: {StaffID}", id);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("❌ API: Invalid model state for staff update");
            return BadRequest(new ApiResponse<object>(false, "Validation failed", ModelState));
        }

        try
        {
            var existingStaff = _repository.Get(id);
            if (existingStaff == null)
            {
                _logger.LogWarning("⚠️ API: Staff not found for update with ID: {StaffID}", id);
                return NotFound(new ApiResponse<object>(false, $"Staff member with ID {id} not found"));
            }

            // Apply updates onto a new Staff instance for validation
            var updated = new Staff
            {
                StaffID = existingStaff.StaffID,
                StaffName = updateStaffDto.StaffName,
                Email = updateStaffDto.Email,
                PhoneNumber = updateStaffDto.PhoneNumber ?? string.Empty,
                StartingDate = updateStaffDto.StartingDate,
                PhotoPath = existingStaff.PhotoPath
            };

            (bool isValidUpdate, System.Collections.Generic.List<string> updateErrors) = _validatorService.ValidateAll(updated);
            if (!isValidUpdate)
            {
                _logger.LogWarning("❌ API: Hybrid validation failed for staff update: {Errors}", string.Join("; ", updateErrors));
                return BadRequest(new ApiResponse<object>(false, "Validation failed", updateErrors));
            }

            existingStaff.StaffName = updated.StaffName;
            existingStaff.Email = updated.Email;
            existingStaff.PhoneNumber = updated.PhoneNumber;
            existingStaff.StartingDate = updated.StartingDate;

            _repository.Update(existingStaff);

            var staffDto = new StaffDto
            {
                StaffID = existingStaff.StaffID,
                StaffName = existingStaff.StaffName,
                Email = existingStaff.Email,
                PhoneNumber = existingStaff.PhoneNumber,
                StartingDate = existingStaff.StartingDate,
                PhotoPath = existingStaff.PhotoPath
            };

            _logger.LogInformation("✅ API: Staff updated successfully: {StaffName}", existingStaff.StaffName);
            return Ok(new ApiResponse<StaffDto>(true, "Staff updated successfully", staffDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 API: Error updating staff with ID: {StaffID}", id);
            return StatusCode(500, new ApiResponse<object>(false, "Internal server error occurred"));
        }
    }

    /// <summary>
    /// Delete a staff member
    /// </summary>
    /// <param name="id">Staff member ID</param>
    /// <returns>Confirmation of deletion</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = "Manager")] // Only managers and admins can delete
    public IActionResult DeleteStaff(int id)
    {
        _logger.LogInformation("🗑️ API: Deleting staff with ID: {StaffID}", id);
        
        try
        {
            var staff = _repository.Get(id);
            if (staff == null)
            {
                _logger.LogWarning("⚠️ API: Staff not found for deletion with ID: {StaffID}", id);
                return NotFound(new ApiResponse<object>(false, $"Staff member with ID {id} not found"));
            }

            _repository.Delete(id);

            _logger.LogInformation("✅ API: Staff deleted successfully: {StaffName}", staff.StaffName);
            return Ok(new ApiResponse<object>(true, "Staff deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 API: Error deleting staff with ID: {StaffID}", id);
            return StatusCode(500, new ApiResponse<object>(false, "Internal server error occurred"));
        }
    }

    /// <summary>
    /// Search staff members
    /// </summary>
    /// <param name="searchTerm">Search term to find staff by name or email</param>
    /// <returns>Matching staff members</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<StaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult SearchStaff([FromQuery, Required] string searchTerm)
    {
        _logger.LogInformation("🔍 API: Searching staff with term: {SearchTerm}", searchTerm);
        
        try
        {
            var staff = _repository.Search(searchTerm);
            var staffDto = staff.Select(s => new StaffDto
            {
                StaffID = s.StaffID,
                StaffName = s.StaffName,
                Email = s.Email,
                PhoneNumber = s.PhoneNumber,
                StartingDate = s.StartingDate,
                PhotoPath = s.PhotoPath
            });

            _logger.LogInformation("✅ API: Found {Count} staff members for search term: {SearchTerm}", 
                staff.Count(), searchTerm);

            return Ok(new ApiResponse<IEnumerable<StaffDto>>(true, "Search completed successfully", staffDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 API: Error searching staff with term: {SearchTerm}", searchTerm);
            return StatusCode(500, new ApiResponse<object>(false, "Internal server error occurred"));
        }
    }
}

// DTOs for API
public class StaffDto
{
    public int StaffID { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime StartingDate { get; set; }
    public string? PhotoPath { get; set; }
}

public class CreateStaffDto
{
    [Required]
    [StringLength(100)]
    public string StaffName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(15)]
    public string? PhoneNumber { get; set; }

    [Required]
    public DateTime StartingDate { get; set; } = DateTime.Now;
}

public class UpdateStaffDto
{
    [Required]
    [StringLength(100)]
    public string StaffName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(15)]
    public string? PhoneNumber { get; set; }

    [Required]
    public DateTime StartingDate { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ApiResponse(bool success, string message, T? data = default)
    {
        Success = success;
        Message = message;
        Data = data;
    }
}