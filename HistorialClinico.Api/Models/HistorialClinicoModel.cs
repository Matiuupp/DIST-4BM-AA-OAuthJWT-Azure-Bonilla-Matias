using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HistorialClinico.Api.Models
{
    [Table("tbl_historialclinico")]
    public class HistorialClinicoModel
    {
        [Key]
        [Column("hist_id")]
        public int IdHistorial { get; set; }

        // Id del paciente en la OTRA base de datos.
        // No lleva ForeignKey: es un dato suelto, la relacion es logica
        [Required]
        [Column("hist_paciente_id")]
        public int IdPaciente { get; set; }

        [Required]
        [StringLength(20)]
        [Column("hist_numero")]
        public string NumHistoria { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Column("hist_diagnostico")]
        public string Diagnostico { get; set; } = string.Empty;

        [StringLength(500)]
        [Column("hist_tratamiento")]
        public string? Tratamiento { get; set; }

        [Column("hist_fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("hist_estado")]
        public bool Estado { get; set; } = true;
    }
}