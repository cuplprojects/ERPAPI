using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        public string? MessageContent { get; set; }

        public int Duration { get; set; } //seconds

/*        public int Expiration { get; set; } //seconds*/

        public int RepeatInterval { get; set; } //seconds

        public int TypeID { get; set; } 

        public DateTime? PostedAt { get; set; } //timestamp

        public DateTime? LastTriggered { get; set; }

        public int UserIdPushedBy { get; set; } 
    }
}
