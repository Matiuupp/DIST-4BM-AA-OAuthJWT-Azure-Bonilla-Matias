using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HistorialClinico.Api.Data;
using HistorialClinico.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace HistorialClinico.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HistorialClinicoController : ControllerBase
    {
        private readonly HistorialDBContext _dbContext;

        public HistorialClinicoController(HistorialDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET: api/HistorialClinico
        // Authorize a secas: basta con tener un token valido, sin importar el rol
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<HistorialClinicoModel>>> GetHistoriales()
        {
            var historiales = await _dbContext.Historiales
                .AsNoTracking()
                .Where(h => h.Estado)   // solo historiales activos
                .ToListAsync();

            return Ok(historiales);
        }

        // GET: api/HistorialClinico/{id}
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<HistorialClinicoModel>> GetHistorial(int id)
        {
            var historial = await _dbContext.Historiales
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.IdHistorial == id);

            if (historial == null)
                return NotFound();

            return Ok(historial);
        }

        // GET: api/HistorialClinico/paciente/{idPaciente}
        // Aqui se ve la relacion 1:N: un paciente tiene muchos historiales
        [Authorize]
        [HttpGet("paciente/{idPaciente}")]
        public async Task<ActionResult<IEnumerable<HistorialClinicoModel>>> GetHistorialesPorPaciente(int idPaciente)
        {
            var historiales = await _dbContext.Historiales
                .AsNoTracking()
                .Where(h => h.IdPaciente == idPaciente && h.Estado)
                .ToListAsync();

            if (!historiales.Any())
                return NotFound();

            return Ok(historiales);
        }

        // POST: api/HistorialClinico
        // Roles = "Administrador": ademas del token, exige que traiga ese rol
        // en sus claims. Un usuario con rol distinto recibe 403
        [Authorize(Roles = "Administrador")]
        [HttpPost]
        public async Task<ActionResult<HistorialClinicoModel>> CrearHistorial([FromBody] HistorialClinicoModel historial)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // El numero de historia es UNIQUE en la BD
            if (await _dbContext.Historiales.AnyAsync(h => h.NumHistoria == historial.NumHistoria))
                return BadRequest("Ya existe un historial con ese numero");

            historial.Estado = true;

            _dbContext.Historiales.Add(historial);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetHistorial), new { id = historial.IdHistorial }, historial);
        }

        // PUT: api/HistorialClinico/{id}
        // Aqui se edita el diagnostico y el tratamiento
        [Authorize(Roles = "Administrador")]
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarHistorial(int id, [FromBody] HistorialClinicoModel historial)
        {
            if (id != historial.IdHistorial)
                return BadRequest();

            _dbContext.Entry(historial).State = EntityState.Modified;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _dbContext.Historiales.AnyAsync(h => h.IdHistorial == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/HistorialClinico/{id}
        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarHistorial(int id)
        {
            var historial = await _dbContext.Historiales.FindAsync(id);
            if (historial == null)
                return NotFound();

            // Borrado logico, igual que en Pacientes
            historial.Estado = false;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }
}