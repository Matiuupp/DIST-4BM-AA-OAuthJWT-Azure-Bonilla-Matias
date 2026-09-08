using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Pacientes.Api.Models;

namespace Pacientes.Api.Services
{
    public class RabbitMQPublisher
    {
        private readonly IConfiguration _configuration;

        public RabbitMQPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Se llama cuando se crea un paciente nuevo
        public async Task PublicarPacienteCreadoAsync(PacienteModel paciente)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]!),
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"]
            };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var queueName = _configuration["RabbitMQ:QueueCreado"]!;

            // Declara la cola por si no existe todavia.
            // durable: true = la cola sobrevive si RabbitMQ se reinicia
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // El objeto viaja como texto JSON, no como objeto de C#
            var mensaje = JsonSerializer.Serialize(paciente);
            var body = Encoding.UTF8.GetBytes(mensaje);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                body: body
            );
        }

        // Se llama cuando un paciente se desactiva (borrado logico)
        public async Task PublicarPacienteDesactivadoAsync(PacienteModel paciente)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]!),
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"]
            };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // Cola distinta: cada evento viaja por su propio canal
            var queueName = _configuration["RabbitMQ:QueueDesactivado"]!;

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var mensaje = JsonSerializer.Serialize(paciente);
            var body = Encoding.UTF8.GetBytes(mensaje);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                body: body
            );
        }
    }
}