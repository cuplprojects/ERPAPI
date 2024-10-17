using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace ERPAPI.Model
{
    public class QuantitySheet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QuantitySheetId { get; set; }

        [Required]
        public string CatchNo { get; set; }
        public string? Paper {  get; set; }
        
       // public DateOnly? ExamDate { get; set; }

       // public TimeOnly? ExamTime { get; set; }

       // public TimeOnly? ExamDuration { get; set; }

        public string Course { get; set; }
        public string Subject { get; set; }
        public string? InnerEnvelope { get; set; }
        public string? OuterEnvelope { get; set; }

        public string? LotNo { get; set; }

        public double Quantity { get; set; }

        public double PercentageCatch { get; set; }

        public int ProjectId { get; set; }

        public bool IsOverridden { get; set; }
       
       public List<int> ProcessId { get; set; } = new List<int>();
    }

   
}
