using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pacientes.Api.Data;
using Pacientes.Api.Services;
using System.Text;

namespace Pacientes.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();

            builder.Services.AddScoped<RabbitMQPublisher>();

            builder.Services.AddDbContext<PacientesDBContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("PacientesConnection")
                ));

            // Configura la validacion del token emitido por OAuthJWT.
            // Los cuatro parametros deben coincidir con los del emisor
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,              // quien emitio el token
                        ValidateAudience = true,            // para quien es el token
                        ValidateLifetime = true,            // que no este expirado
                        ValidateIssuerSigningKey = true,    // que la firma sea autentica

                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
                        )
                    };
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // El orden importa: primero se identifica QUIEN eres (Authentication),
            // despues se revisa QUE puedes hacer (Authorization).
            // Invertirlos hace que todas las peticiones fallen con 401
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}