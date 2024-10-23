using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProjectId { get; set; }
        public int GroupId { get; set; }
        public int TypeId { get; set; }
        public string Name { get; set; }
       
        public bool Status { get; set; }
        public string Description { get; set; }
       
    }

   
}
