namespace HistorialClinico.Api.Models
{
    // Copia local de los datos que llegan por RabbitMQ.
    
    public class PacienteEvento
    {
        public int IdPaciente { get; set; }
        public string Cedula { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
    }
}