namespace CAKA.PerformanceApp.Models.Api;

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? Department { get; set; }
    public string? Role { get; set; }
    public string? Error { get; set; }
}
