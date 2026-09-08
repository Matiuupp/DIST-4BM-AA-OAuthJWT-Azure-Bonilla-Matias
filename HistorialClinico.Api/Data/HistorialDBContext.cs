using Microsoft.EntityFrameworkCore;
using HistorialClinico.Api.Models;

namespace HistorialClinico.Api.Data
{
    public class HistorialDBContext : DbContext
    {
        // Constructor que trae las opciones del EntityFramework
        public HistorialDBContext(DbContextOptions<HistorialDBContext> options) : base(options)
        {
        }

        public DbSet<HistorialClinicoModel> Historiales { get; set; }
    }
}