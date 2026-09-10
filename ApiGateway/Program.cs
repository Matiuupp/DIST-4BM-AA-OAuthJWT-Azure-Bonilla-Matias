namespace ApiGateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Carga la configuracion de rutas y clusters desde appsettings.json
            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            var app = builder.Build();

            // Endpoint de salud en la raiz.
            // Sin esto, entrar a la URL base devuelve un 404 porque el gateway
            // solo conoce las rutas que empiezan con /api/
            app.MapGet("/", () => Results.Json(new
            {
                servicio = "API Gateway - Microservicios Pacientes",
                estado = "Activo",
                rutas = new[]
                {
                    "POST /api/Auth/login",
                    "GET  /api/Pacientes",
                    "GET  /api/HistorialClinico"
                }
            }));

            // Activa el enrutamiento: aqui es donde YARP intercepta las peticiones
            app.MapReverseProxy();

            app.Run();
        }
    }
}