using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("token")]
    public IActionResult GenerateToken([FromBody] Dictionary<string, string> body)
    {
        try
        {
            if (body == null || !body.ContainsKey("username"))
                return BadRequest(new { message = "username required" });

            var username = body["username"];

            var keyString = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(keyString))
                return StatusCode(500, new { message = "JWT Key missing in configuration" });

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: new[] { new Claim("username", username) },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { token = tokenString });
        }
        catch (Exception ex)
        {
            Console.WriteLine("AUTH TOKEN ERROR ===> " + ex.ToString());
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
