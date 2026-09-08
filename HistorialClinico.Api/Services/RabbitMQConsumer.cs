using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using HistorialClinico.Api.Data;
using HistorialClinico.Api.Models;

namespace HistorialClinico.Api.Services
{
    // BackgroundService: corre en segundo plano mientras la API vive, escuchando la cola sin bloquear los endpoints
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMQConsumer(IConfiguration configuration, IServiceProvider serviceProvider)
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

            // Cola de pacientes creados
            var queueName = _configuration["RabbitMQ:QueueCreado"]!;

            // Los mismos parametros que uso el publisher, si no coinciden RabbitMQ rechaza
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            var consumer = new AsyncEventingBasicConsumer(channel);

            // Se dispara cada vez que llega un mensaje a la cola
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
                        // El consumidor vive fuera del ciclo de una peticion HTTP
                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<HistorialDBContext>();

                        var historial = new HistorialClinicoModel
                        {
                            IdPaciente = paciente.IdPaciente,
                            NumHistoria = $"HC-{paciente.IdPaciente:D4}",
                            Diagnostico = "Sin diagnostico",
                            Tratamiento = null,
                            Fecha = DateTime.Now,
                            Estado = true
                        };

                        dbContext.Historiales.Add(historial);
                        await dbContext.SaveChangesAsync();
                    }

                    // Todo salio bien: recien ahora confirmamos y RabbitMQ borra el mensaje
                    await channel.BasicAckAsync(evento.DeliveryTag, multiple: false);
                }
                catch (Exception)
                {
                    // Algo fallo: devolvemos el mensaje a la cola para reintentarlo despues requeue: true = vuelve a la cola en vez de descartarse
                    await channel.BasicNackAsync(evento.DeliveryTag, multiple: false, requeue: true);
                }
            };

            // El BasicConsume va FUERA del evento: se ejecuta una sola vez al arrancar y deja al consumidor escuchando la cola
            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );
        }
    }
}