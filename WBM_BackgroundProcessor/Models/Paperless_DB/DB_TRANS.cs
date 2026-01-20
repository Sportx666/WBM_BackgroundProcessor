using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class DB_TRANS
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string TRANS_NO { get; set; }
        public string OWNER_ID { get; set; }
        public string ACTION { get; set; }
        public Nullable<System.DateTime> TRANS_DATE { get; set; }
        public Nullable<System.TimeSpan> TRANS_TIME { get; set; }
        public Nullable<decimal> QUANTITY { get; set; }
        public string REFERENCE { get; set; }
        public string PICK_TYPE { get; set; }
        public string TRANS_CODE { get; set; }
        public Nullable<decimal> RATE { get; set; }
        public Nullable<decimal> TRANS_AMT { get; set; }
        public string INVOICE_NO { get; set; }
        public string DESCRIPTION { get; set; }
        public Nullable<System.DateTime> DATE_STAMP { get; set; }
        public Nullable<System.TimeSpan> TIME_STAMP { get; set; }
        public string WHCODE { get; set; }
        public Nullable<decimal> GST { get; set; }
        public string ZONE { get; set; }
        public string PALLET { get; set; }
        public Nullable<decimal> INV_LINE_NO { get; set; }

    }
}
