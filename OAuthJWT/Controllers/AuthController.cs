using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OAuthJWT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // POST: api/Auth/login
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest loginRequest)
        {
            string rol;

            // Usuarios quemados en codigo. En un sistema real irian en una
            // base de datos y con la contrasenia cifrada
            if (loginRequest.Usuario == "admin" && loginRequest.Password == "1234")
            {
                rol = "Administrador";
            }
            else if (loginRequest.Usuario == "medico" && loginRequest.Password == "1234")
            {
                rol = "Usuario";
            }
            else
            {
                return Unauthorized("Usuario o contraseña incorrectos");
            }

            // Los claims son los datos que viajan DENTRO del token.
            // Cualquiera puede leerlos, por eso nunca se ponen contrasenias aqui
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, loginRequest.Usuario),
                new Claim(ClaimTypes.Role, rol),
            };

            // La clave secreta que firma el token. Debe ser identica
            // en los microservicios que lo van a validar
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)
            );

            var credenciales = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(
                    Convert.ToDouble(_configuration["Jwt:ExpireMinutes"])
                ),
                signingCredentials: credenciales
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                usuario = loginRequest.Usuario,
                rol = rol,
            });
        }

        public class LoginRequest
        {
            public string Usuario { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }
}