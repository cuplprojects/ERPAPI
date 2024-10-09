using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class FeatureEnabling
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ModuleId { get; set; }

        public int FeatureId { get; set; }
        public bool IsEnabled { get; set; }
        public bool Independent { get; set; }
        public int ProcessGroupId { get; set; }
    }
}
