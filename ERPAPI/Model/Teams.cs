using System;
using System.Collections.Generic; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class Teams
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TeamId { get; set; }

        [Required]
        public string TeamName { get; set; }

        public string Description { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool Status { get; set; } = true;

        
        public List<int> UserIds { get; set; } = new List<int>();
    }
}