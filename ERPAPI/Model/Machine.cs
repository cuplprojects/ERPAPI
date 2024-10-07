using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class Machine
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MachineId { get; set; }
        public string MachineName { get; set; }
        public string MachineType { get; set; }
        public int ZoneId { get; set; }
        public bool Status { get; set; }
        public int DepartmentId { get; set; }
    }
}
