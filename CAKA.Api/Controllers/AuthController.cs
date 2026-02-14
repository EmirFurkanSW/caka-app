using System.Security.Claims;
using CAKA.Api.Data;
using CAKA.Api.Models;
using CAKA.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthTokenService _tokenService;

    public AuthController(AppDbContext db, IAuthTokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return Ok(new LoginResponse { Success = false, Error = "Kullanıcı adı ve şifre gerekli." });

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName.Trim());

        if (user == null)
            return Ok(new LoginResponse { Success = false, Error = "Kullanıcı adı veya şifre hatalı." });

        if (user.IsSuspended)
            return Ok(new LoginResponse { Success = false, Error = "Hesap askıya alınmış." });

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Ok(new LoginResponse { Success = false, Error = "Kullanıcı adı veya şifre hatalı." });

        var token = _tokenService.GenerateToken(user.UserName, user.Role);
        return Ok(new LoginResponse
        {
            Success = true,
            Token = token,
            UserName = user.UserName,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName,
            Department = user.Department ?? "",
            Role = user.Role
        });
    }

    /// <summary>Giriş yapmış herhangi bir kullanıcı (admin veya personel) kendi şifresini değiştirir.</summary>
    [HttpPost("change-my-password")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> ChangeMyPassword([FromBody] ChangePasswordRequest req)
    {
        var userName = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(userName)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        if (user == null) return Forbid();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return Ok(new LoginResponse { Success = false, Error = "Mevcut şifre hatalı." });

        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return Ok(new LoginResponse { Success = false, Error = "Yeni şifre girin." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new LoginResponse { Success = true });
    }

    [HttpPost("change-admin-password")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<LoginResponse>> ChangeAdminPassword([FromBody] ChangePasswordRequest req)
    {
        var userName = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(userName)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        if (user == null || user.Role != "Admin") return Forbid();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return Ok(new LoginResponse { Success = false, Error = "Mevcut şifre hatalı." });

        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return Ok(new LoginResponse { Success = false, Error = "Yeni şifre girin." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new LoginResponse { Success = true });
    }
}
