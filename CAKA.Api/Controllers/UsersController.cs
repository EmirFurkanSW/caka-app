using System.Security.Claims;
using CAKA.Api;
using CAKA.Api.Data;
using CAKA.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOrPersonel")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    private static bool IsCallerFullAdmin(ClaimsPrincipal user) => JwtRoleNormalizer.HasAdmin(user);

    private string? CurrentUserName => User.FindFirstValue(ClaimTypes.Name);

    /// <summary>Admin/yönetici veya en az bir işin proje müdürü kullanıcı listesini çalışan ataması için görebilir.</summary>
    private async Task<bool> CanListUsersForJobAssignmentAsync()
    {
        if (JwtRoleNormalizer.HasAdminOrYonetici(User))
            return true;
        var me = CurrentUserName;
        if (string.IsNullOrEmpty(me))
            return false;
        return await _db.Jobs.AsNoTracking()
            .AnyAsync(j => j.ProjectManagerUserName != null &&
                           j.ProjectManagerUserName.ToLower() == me.ToLower());
    }

    [HttpGet]
    [Authorize(Policy = "AdminOrPersonel")]
    public async Task<ActionResult<List<StoredUserDto>>> GetAll()
    {
        if (!await CanListUsersForJobAssignmentAsync())
            return Forbid();

        var list = await _db.Users
            .Where(u => u.Role != "Admin")
            .OrderBy(u => u.UserName)
            .Select(u => new StoredUserDto
            {
                UserName = u.UserName,
                Password = "", // Şifre API'den gönderilmez
                DisplayName = u.DisplayName,
                Department = u.Department,
                HourlyRate = u.HourlyRate,
                IsSuspended = u.IsSuspended,
                Role = u.Role ?? "Personel"
            })
            .ToListAsync();
        return list;
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<LoginResponse>> Add([FromBody] StoredUserDto dto)
    {
        var userName = (dto.UserName ?? "").Trim();
        if (string.IsNullOrEmpty(userName))
            return Ok(new LoginResponse { Success = false, Error = "Kullanıcı adı boş olamaz." });
        if (string.IsNullOrWhiteSpace(dto.Password))
            return Ok(new LoginResponse { Success = false, Error = "Şifre girin." });

        if (await _db.Users.AnyAsync(u => u.UserName == userName))
            return Ok(new LoginResponse { Success = false, Error = "Bu kullanıcı adı zaten kayıtlı." });

        var role = string.IsNullOrWhiteSpace(dto.Role) ? "Personel" : dto.Role.Trim();
        if (role != "Personel" && role != "Yonetici")
            role = "Personel";

        _db.Users.Add(new UserEntity
        {
            UserName = userName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            DisplayName = (dto.DisplayName ?? "").Trim(),
            Department = (dto.Department ?? "").Trim(),
            HourlyRate = dto.HourlyRate < 0 ? 0 : dto.HourlyRate,
            IsSuspended = dto.IsSuspended,
            Role = role
        });
        await _db.SaveChangesAsync();
        return Ok(new LoginResponse { Success = true });
    }

    [HttpDelete("{userName}")]
    [Authorize(Policy = "AdminOrYonetici")]
    public async Task<ActionResult> Delete(string userName)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName && u.Role != "Admin");
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{userName}/suspended")]
    [Authorize(Policy = "AdminOrYonetici")]
    public async Task<ActionResult> SetSuspended(string userName, [FromBody] bool suspended)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName && u.Role != "Admin");
        if (user == null) return NotFound();
        user.IsSuspended = suspended;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{userName}")]
    [Authorize(Policy = "AdminOrYonetici")]
    public async Task<ActionResult<LoginResponse>> UpdateUser(string userName, [FromBody] StoredUserDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName && u.Role != "Admin");
        if (user == null) return NotFound();

        user.DisplayName = (dto.DisplayName ?? "").Trim();
        user.Department = (dto.Department ?? "").Trim();
        user.HourlyRate = dto.HourlyRate < 0 ? 0 : dto.HourlyRate;
        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        if (IsCallerFullAdmin(User) && dto.Role != null && !string.IsNullOrWhiteSpace(dto.Role))
        {
            var r = dto.Role.Trim();
            if (r.Equals("Personel", StringComparison.OrdinalIgnoreCase))
                user.Role = "Personel";
            else if (r.Equals("Yonetici", StringComparison.OrdinalIgnoreCase))
                user.Role = "Yonetici";
        }

        await _db.SaveChangesAsync();
        return Ok(new LoginResponse { Success = true });
    }
}
