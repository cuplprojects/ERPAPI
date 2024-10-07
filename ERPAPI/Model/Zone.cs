using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class Zone
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ZoneId { get; set; }
        public string ZoneNo { get; set; }
        public string ZoneDescription { get; set; }
        public string SortOrder { get; set; }
        public bool Status { get; set; }
    }
}
