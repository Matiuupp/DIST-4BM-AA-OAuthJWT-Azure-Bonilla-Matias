using Microsoft.EntityFrameworkCore;
using Pacientes.Api.Models;

namespace Pacientes.Api.Data
{
    public class PacientesDBContext : DbContext
    {
        // Constructor que trae las opciones del EntityFramework
        public PacientesDBContext(DbContextOptions<PacientesDBContext> options) : base(options)
        {
        }

        public DbSet<PacienteModel> Pacientes { get; set; }
    }
}