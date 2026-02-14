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

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<List<StoredUserDto>>> GetAll()
    {
        var list = await _db.Users
            .Where(u => u.Role != "Admin")
            .OrderBy(u => u.UserName)
            .Select(u => new StoredUserDto
            {
                UserName = u.UserName,
                Password = "", // Şifre API'den gönderilmez
                DisplayName = u.DisplayName,
                Department = u.Department,
                IsSuspended = u.IsSuspended
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

        _db.Users.Add(new UserEntity
        {
            UserName = userName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            DisplayName = (dto.DisplayName ?? "").Trim(),
            Department = (dto.Department ?? "").Trim(),
            IsSuspended = dto.IsSuspended,
            Role = "Personel"
        });
        await _db.SaveChangesAsync();
        return Ok(new LoginResponse { Success = true });
    }

    [HttpDelete("{userName}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Delete(string userName)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName && u.Role != "Admin");
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{userName}/suspended")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> SetSuspended(string userName, [FromBody] bool suspended)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName && u.Role != "Admin");
        if (user == null) return NotFound();
        user.IsSuspended = suspended;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{userName}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<LoginResponse>> UpdateUser(string userName, [FromBody] StoredUserDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName && u.Role != "Admin");
        if (user == null) return NotFound();

        user.DisplayName = (dto.DisplayName ?? "").Trim();
        user.Department = (dto.Department ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        await _db.SaveChangesAsync();
        return Ok(new LoginResponse { Success = true });
    }
}
