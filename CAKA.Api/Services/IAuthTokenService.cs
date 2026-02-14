namespace CAKA.Api.Services;

public interface IAuthTokenService
{
    string GenerateToken(string userName, string role);
}
