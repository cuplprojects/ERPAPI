using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class FeatureEnabling
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FeatureEnablingId { get; set; }
        public int FeatureId { get; set; }
        public bool Enabled { get; set; }
        public bool Independent { get; set; }
        public int ProcessGroupId { get; set; }
    }
}
