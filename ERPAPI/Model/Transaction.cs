using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class Transaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TransactionId { get; set; }
        public int Quantity { get; set; }
        public string Remarks { get; set; }
        public int ProjectId { get; set; }
        public int QuantitysheetId { get; set; }
        public int ProcessId { get; set; }
        public int ZoneId { get; set; }
        public int StatusId { get; set; }
        public int AlarmId { get; set; }
        public int LotNo { get; set; }
    }
}
