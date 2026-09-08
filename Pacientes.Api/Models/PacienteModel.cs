using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pacientes.Api.Models
{
    [Table("tbl_paciente")]  // Nombre de la tabla en la BD
    public class PacienteModel
    {
        [Key]
        [Column("pac_id")]
        public int IdPaciente { get; set; }

        [Required]
        [StringLength(10)]
        [Column("pac_cedula")]
        public string Cedula { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Column("pac_nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Column("pac_apellido")]
        public string Apellido { get; set; } = string.Empty;

        [StringLength(200)]
        [Column("pac_direccion")]
        public string? Direccion { get; set; }

        [Column("pac_estado")]
        public bool Estado { get; set; } = true;
    }
}