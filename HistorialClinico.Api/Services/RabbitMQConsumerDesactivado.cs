using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HistorialClinico.Api.Data;
using HistorialClinico.Api.Models;

namespace HistorialClinico.Api.Services
{
    // Segundo consumidor: escucha la cola de pacientes desactivados
    public class RabbitMQConsumerDesactivado : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMQConsumerDesactivado(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"],
                Port = int.Parse(_configuration["RabbitMQ:Port"]!),
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"]
            };

            var connection = await factory.CreateConnectionAsync(stoppingToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            var queueName = _configuration["RabbitMQ:QueueDesactivado"]!;

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (modelo, evento) =>
            {
                try
                {
                    var body = evento.Body.ToArray();
                    var mensaje = Encoding.UTF8.GetString(body);

                    var paciente = JsonSerializer.Deserialize<PacienteEvento>(
                        mensaje,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (paciente != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<HistorialDBContext>();

                        // Un paciente puede tener VARIOS historiales: los desactivamos todos.
                        // Aqui se ve el 1:N en accion
                        var historiales = await dbContext.Historiales
                            .Where(h => h.IdPaciente == paciente.IdPaciente && h.Estado)
                            .ToListAsync();

                        foreach (var historial in historiales)
                        {
                            historial.Estado = false;
                        }

                        await dbContext.SaveChangesAsync();
                    }

                    await channel.BasicAckAsync(evento.DeliveryTag, multiple: false);
                }
                catch (Exception)
                {
                    await channel.BasicNackAsync(evento.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );
        }
    }
}