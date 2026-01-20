using System.ComponentModel.DataAnnotations;

namespace WBM_BackgroundProcessor.Models.Paperless_DB
{
    public class TRUCK_LOAD_DW
    {
        [Key]
        public string KEYID { get; set; }
        public string SITE_NO { get; set; }
        public string LOAD_NO { get; set; }
        public string SO_NO { get; set; }
        public string PAL_NO { get; set; }
        public Nullable<decimal> PAL_WEIGHT { get; set; }
        public Nullable<decimal> PAL_CUB { get; set; }
        public string PAL_LOAD_FLG { get; set; }
        public string DOCK_NO { get; set; }
        public string MANIF_NO { get; set; }
        public string TRUCK_SIGN { get; set; }
        public string CARRIER_CODE { get; set; }
        public string TRUCK_NO { get; set; }
        public string CONTAINER { get; set; }
        public string SEAL_NO { get; set; }
        public string ROUTE_CODE { get; set; }
        public Nullable<decimal> NO_PALS { get; set; }
        public Nullable<decimal> NO_CARTONS { get; set; }
        public string ACT_PALLET { get; set; }
        public string PAL_LIST { get; set; }
        public string PAL_SO_NO { get; set; }
        public Nullable<System.DateTime> CONF_DATE { get; set; }
        public Nullable<System.DateTime> LOADED_DATE { get; set; }
        public string NO_UNITS { get; set; }
        public Nullable<System.DateTime> DEL_DATE { get; set; }
        public Nullable<System.TimeSpan> DEL_TIME { get; set; }
        public string ACTION { get; set; }
        public Nullable<System.DateTime> ACTION_DATE { get; set; }
        public Nullable<System.TimeSpan> ACTION_TIME { get; set; }
        public string ACTION_USER { get; set; }
        public string MOBILE { get; set; }
        public string RF_PAL_TYPE_ENTERED { get; set; }
        public string RF_PAL_TYPE_COUNTED { get; set; }
        public string CARRIER_SERVICE_CODE { get; set; }

    }
}
