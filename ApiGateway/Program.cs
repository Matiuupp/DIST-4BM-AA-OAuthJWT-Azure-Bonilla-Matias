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

            // Activa el enrutamiento: aqui es donde YARP intercepta las peticiones
            app.MapReverseProxy();

            app.Run();
        }
    }
}