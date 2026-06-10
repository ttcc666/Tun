using System.ComponentModel.DataAnnotations;

namespace Tun.Server.Domain.Configuration;

public class ServerOptions
{
    [Required(ErrorMessage = "BaseDomain 不能为空")]
    [RegularExpression(@"^[a-z0-9.-]+$", ErrorMessage = "BaseDomain 格式无效")]
    public string BaseDomain { get; set; } = "localhost";

    [Required(ErrorMessage = "Token 不能为空")]
    [MinLength(8, ErrorMessage = "Token 长度至少 8 位")]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "ManagementToken 不能为空")]
    [MinLength(8, ErrorMessage = "ManagementToken 长度至少 8 位")]
    public string ManagementToken { get; set; } = "";

    public bool RequireServerConfig { get; set; } = false;
}
