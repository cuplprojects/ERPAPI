using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class ProjectProcessFeature
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProjectProcessFeatureId { get; set; }
        public int ProjectProcessId { get; set; }
      
        public int FeatureId { get; set; }
        public bool Independent { get; set; }

    }
}
