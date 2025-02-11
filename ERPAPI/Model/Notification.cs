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

        public int ExpirationTime { get; set; } //seconds

        public int RepeatInterval { get; set; } //seconds

        public int UserIdPushedBy { get; set; } 
    }
}
