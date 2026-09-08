using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pacientes.Api.Data;
using Pacientes.Api.Models;
using Pacientes.Api.Services;
using Microsoft.AspNetCore.Authorization;


namespace Pacientes.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PacientesController : ControllerBase
    {
        private readonly PacientesDBContext _dbContext;
        private readonly RabbitMQPublisher _rabbitMQPublisher;

        // Inyeccion de dependencias: ASP.NET nos entrega el contexto y el publisher ya listos
        public PacientesController(PacientesDBContext dbContext, RabbitMQPublisher rabbitMQPublisher)
        {
            _dbContext = dbContext;
            _rabbitMQPublisher = rabbitMQPublisher;
        }

        // GET: api/Pacientes
        // Authorize a secas: basta con tener un token valido, sin importar el rol
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PacienteModel>>> GetPacientes()
        {
            // AsNoTracking: solo lectura, no vigila cambios y la consulta es mas rapida
            var pacientes = await _dbContext.Pacientes
                .AsNoTracking()
                .Where(p => p.Estado)   // solo los pacientes activos
                .ToListAsync();

            return Ok(pacientes);
        }

        // GET: api/Pacientes/{id}
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<PacienteModel>> GetPaciente(int id)
        {
            var paciente = await _dbContext.Pacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdPaciente == id);

            if (paciente == null)
                return NotFound();

            return Ok(paciente);
        }

        // POST: api/Pacientes
        // Roles = "Administrador": ademas del token, exige que traiga ese rol
        // en sus claims. Un usuario con rol distinto recibe 403
        [Authorize(Roles = "Administrador")]
        [HttpPost]
        public async Task<ActionResult<PacienteModel>> CrearPaciente([FromBody] PacienteModel paciente)
        {
            // Valida las anotaciones del modelo: Required, StringLength, etc.
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // La cedula es UNIQUE en la BD: validamos antes para dar un mensaje claro
            if (await _dbContext.Pacientes.AnyAsync(p => p.Cedula == paciente.Cedula))
                return BadRequest("Ya existe un paciente con esa cedula");

            paciente.Estado = true;   // todo paciente nuevo nace activo

            _dbContext.Pacientes.Add(paciente);
            await _dbContext.SaveChangesAsync();

            // Publica el evento para que HistorialClinico.Api cree el historial inicial.
            // Va DESPUES del SaveChanges porque el paciente ya debe tener su IdPaciente asignado
            await _rabbitMQPublisher.PublicarPacienteCreadoAsync(paciente);

            return CreatedAtAction(nameof(GetPaciente), new { id = paciente.IdPaciente }, paciente);
        }

        // PUT: api/Pacientes/{id}
        [Authorize(Roles = "Administrador")]
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarPaciente(int id, [FromBody] PacienteModel paciente)
        {
            if (id != paciente.IdPaciente)
                return BadRequest();

            // Marca la entidad como modificada para que EF genere el UPDATE
            _dbContext.Entry(paciente).State = EntityState.Modified;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Si el registro ya no existe devolvemos 404 en vez de reventar
                if (!await _dbContext.Pacientes.AnyAsync(p => p.IdPaciente == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Pacientes/{id}
        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarPaciente(int id)
        {
            var paciente = await _dbContext.Pacientes.FindAsync(id);
            if (paciente == null)
                return NotFound();

            // Borrado logico: no se elimina la fila, se marca como inactiva
            // para no perder el historial medico asociado
            paciente.Estado = false;
            await _dbContext.SaveChangesAsync();

            // Avisa a HistorialClinico.Api para que desactive los historiales de este paciente
            await _rabbitMQPublisher.PublicarPacienteDesactivadoAsync(paciente);

            return NoContent();
        }
    }
}