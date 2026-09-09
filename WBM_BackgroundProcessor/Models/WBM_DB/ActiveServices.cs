using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.WBM_DB
{
    public class ActiveServices
    {
        [Key]
        public int ID { get; set; }
        public DateTime LogDateTime { get; set; }
        public int ApplicationID { get; set; }
        public string ApplicationName { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; }
        public DateTime NextStatusDue { get; set; }

    }
}