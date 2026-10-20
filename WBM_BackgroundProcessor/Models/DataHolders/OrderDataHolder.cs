using Microsoft.Extensions.Primitives;
using NLog;
using NLog.Targets;
using NLog.Web;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using System.Text;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using WBM_BackgroundProcessor.Polling;
using wbm_common.DataObjects;
using wbm_common.NonDatabaseObjects;
using wbm_common.Paperless_DB_DataObjects;

namespace WBM_BackgroundProcessor.Models.DataHolders
{
    public class OrderDataHolder
    {
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        public OrderDataHolder(int CompanyID, int SiteID, int OwnerID)
        {
            DictActionRateCodeStat = new Dictionary<string, ActionRateCodeStat>();

            this.CompanyID = CompanyID;
            this.SiteID = SiteID;
            this.OwnerID = OwnerID;
        }

        #region Subclasses
        public class ActionRateCodeStat
        {
            public int ActionTypeID { get; set; }

            public int UnitTypeID { get; set; }

            public string ActionChargeCode { get; set; }

            public int Quantity { get; set; }

            public string Key
            {
                get
                {
                    return ActionTypeID.ToString() + "-" + UnitTypeID.ToString() + ActionChargeCode;
                }
            }
        }

        public class ChargeDetails
        {
            public int RateChargeCodeID { get; set; }
            public string RateChargeCode { get; set; }
            public string Desciption { get; set; }

            public ChargeDetails(int RateChargeCodeID, string RateChargeCode, string Description)
            {
                this.RateChargeCodeID = RateChargeCodeID;
                this.RateChargeCode = RateChargeCode;
                this.Desciption = Description;
            }
        }

        #endregion

        public int OwnerID { get; set; }

        public string Owner_SourceOwnerID { get; set; }

        public int CompanyID { get; set; }

        public int SiteID { get; set; }

        public int CarrierID { get; set; }

        #region Paperless Datatables

        // Paperless Datatables

        public PICK_HEAD_DW PickHeadDW { get; set; }

        private List<PICK_DETAIL_DW> _ListPickDetailDW;
        public List<PICK_DETAIL_DW> ListPickDetailDW
        {
            get { return _ListPickDetailDW; }
            set
            {
                _ListPickDetailDW = value;
            }
        }

        private List<STOCK_MOVE_DW> _ListStockMoveDW;

        public List<STOCK_MOVE_DW> ListStockMoveDW
        {
            get { return _ListStockMoveDW; }
            set
            {
                _ListStockMoveDW = value;
                SetListPALNO();
                SetListSKU();
            }
        }

        public List<string> ListPALNO { get; set; }

        public List<DB_TRANS> ListDBTrans { get; set; }

        public List<string> ListSKU { get; set; }

        public List<TRUCK_LOAD_DW> ListTruckLoadDW { get; set; }
        int PalletCountFromTruckLoad { get; set; }
        int PalletCountFromPickHead { get; set; }

        #endregion

        #region Warehouse Billing Module Datatables

        // Warehouse Billing Module Datatables

        public List<ActionRateCode.dbRow> ListActionRateCode { get; set; }

        public List<PriorityOrder.dbRow> ListPriorityOrder { get; set; }

        public List<CustomerAddressDefault.dbRow> ListCustomerAddressDefault { get; set; }
        private CustomerAddressDefault.dbRow CADUsed { get; set; }

        public CustomerAddress.dbRow CustomerAddress { get; set; }

        public List<Stock.dbRow> ListStock { get; set; }

        public List<ConnoteFee.dbRow> ListConnoteFee { get; set; }

        public List<Carrier.dbRow> ListCarrier { get; set; }
        public TransactionHolder TransactionHolder { get; set; }

        public List<RateCollection.dbRow> ListRateCollection { get; set; }

        public RateCode.dbRow MinCharge_RateCode { get; set; }

        public ZoneProcessing.dbRow ZoneProcessing { get; set; }

        public List<ProcessingRule.dbRow> ListProcessingRule { get; set; }

        #endregion

        private string _PaperlessOrderNumber = "";
        public string PaperlessOrderNumber
        {
            get
            {
                if (_PaperlessOrderNumber == "")
                {
                    if (PickHeadDW.PHEAD_ID.Length > 5)
                    {
                        _PaperlessOrderNumber = PickHeadDW.PHEAD_ID.Substring(5, PickHeadDW.PHEAD_ID.Length - 5);
                    }
                }
                return _PaperlessOrderNumber;
            }
        }

        public int SKUCount
        {
            get
            {
                if (ListSKU != null)
                {
                    return ListSKU.Count;
                }
                else
                {
                    return 0;
                }
            }
        }

        public Decimal TotalCartonCount
        {
            get
            {
                Decimal cartonCount = 0;
                foreach (PICK_DETAIL_DW pd in ListPickDetailDW)
                {
                    if (pd.QTY_ORD != null)
                    {
                        cartonCount += pd.QTY_ORD.Value;
                    }
                }

                return cartonCount;
            }
        }

        public int Pick_FullPalletCount { get; set; }
        public Decimal Pick_CartonCount { get; set; }

        public Decimal Pick_TotalCartonCount { get; set; }

        private Boolean Calculated_IsPriority { get; set; }

        public int WrapOut_Pal { get; set; }

        public int ConnoteLabelCount { get; set; }
        public Boolean ChargeInvoiceEnclosed { get; set; }

        public int PalletHireDelayDays { get; set; }

        private int ActionTypeID_Pick = 1;
        private int ActionTypeID_HandleOut = 4;
        private int ActionTypeID_LoadOut = 5;
        private int UnitTypeID_Carton = 1;
        private int UnitTypeID_Pallet = 2;
        private int UnitTypeID_Layer = 3;

        public Dictionary<string, ActionRateCodeStat> DictActionRateCodeStat { get; set; }

        public StringBuilder debugString = new StringBuilder();

        // Processing
        #region processing

        public void SetListPALNO()
        {
            // Build list of Pallet Licennce plates
            ListPALNO = new List<string>();

            if (_ListStockMoveDW != null)
            {
                foreach (STOCK_MOVE_DW sm in _ListStockMoveDW)
                {
                    if (!ListPALNO.Contains(sm.PAL_NO))
                    {
                        ListPALNO.Add(sm.PAL_NO);
                    }
                }
            }
        }

        public void SetListSKU()
        {
            // Build list of SKUs
            ListSKU = new List<string>();
            if (ListStockMoveDW != null)
            {
                foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
                {
                    if (!ListSKU.Contains(sm.PRODUCT_CODE))
                    {
                        ListSKU.Add(sm.PRODUCT_CODE);
                    }
                }
            }
        }

        public void SetCarrierID()
        {
            if (ListCarrier != null)
            {
                Carrier.dbRow this_carrier = ListCarrier.Where(r => r.SiteID == SiteID && r.PublicID == PickHeadDW.CARRIER_CODE).FirstOrDefault();
                if (this_carrier != null)
                    CarrierID = this_carrier.ID;
            }
        }

        public void PreProcessData()
        {
            CalculateOrderPriority();
            ProcessCustomerAddress();
            SetCarrierID();
        }

        public Boolean Order_Can
        {
            get
            {
                if (PickHeadDW != null)
                {
                    if (PickHeadDW.PHEAD_STATUS == "10")
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }
        }

        private void CalculateOrderPriority()
        {
            Calculated_IsPriority = false;
            PriorityOrder.dbRow found_row = null;

            if (ListPriorityOrder != null)
            {
                Boolean DisqualifyRule = false;

                foreach (PriorityOrder.dbRow po in ListPriorityOrder)
                {
                    // List is ordered by owner then site. 
                    // we will first choose the generic no owner, no site,
                    // then if there is a site,
                    // then if there is an owner,
                    // lastly if there is an owner and site

                    DisqualifyRule = false;


                    if (po.OrderPriority != PickHeadDW.PTY_NO)
                    {
                        // Not same priority
                        DisqualifyRule = true;
                    }

                    if (CompanyID > 0 && po.CompanyID != CompanyID)
                    {
                        // Not same company
                        DisqualifyRule = true;
                    }

                    if (!DisqualifyRule && (po.OwnerID > 0 && po.OwnerID != OwnerID))
                    {
                        // Not same owner
                        DisqualifyRule = true;
                    }

                    if (!DisqualifyRule && (po.SiteID > 0 && po.SiteID != SiteID))
                    {
                        // Not same site
                        DisqualifyRule = true;
                    }

                    if (!DisqualifyRule)
                    {
                        found_row = po;
                    }
                }

                if (found_row != null)
                {
                    Calculated_IsPriority = true;
                }
            }

        }

        private void ProcessCustomerAddress()
        {
            Boolean GotInvEncl = false;
            Boolean GotPalletHireDelayDays = false;

            if (CustomerAddress != null)
            {
                if (CustomerAddress.InvoiceEnclosed.HasValue)
                {
                    ChargeInvoiceEnclosed = CustomerAddress.InvoiceEnclosed.Value;
                    GotInvEncl = true;
                }

                if (CustomerAddress.PalletHireDelayDays.HasValue)
                {
                    PalletHireDelayDays = CustomerAddress.PalletHireDelayDays.Value;
                    GotPalletHireDelayDays = true;
                }
            }

            if (!GotInvEncl || !GotPalletHireDelayDays)
            {
                CustomerAddressDefault.dbRow found_row = null;
                Boolean DisqualifyRule = false;

                foreach (CustomerAddressDefault.dbRow cad in ListCustomerAddressDefault)
                {
                    // List is ordered by owner then site. 
                    // we will first choose the generic no owner, no site,
                    // then if there is a site,
                    // then if there is an owner,
                    // lastly if there is an owner and site

                    DisqualifyRule = false;

                    if (cad.CompanyID > 0 && cad.CompanyID != CompanyID)
                    {
                        DisqualifyRule = true;
                    }

                    if (!DisqualifyRule && (cad.OwnerID > 0 && cad.OwnerID != OwnerID))
                    {
                        DisqualifyRule = true;
                    }

                    if (!DisqualifyRule && (cad.SiteID > 0 && cad.SiteID != SiteID))
                    {
                        DisqualifyRule = true;
                    }

                    if (!DisqualifyRule)
                        found_row = cad;
                }
                if (found_row != null)
                {
                    CADUsed = found_row;
                    if (!GotInvEncl)
                        ChargeInvoiceEnclosed = found_row.InvoiceEnclosed;

                    if (!GotPalletHireDelayDays)
                        PalletHireDelayDays = found_row.PalletHireDelayDays;
                }
            }
        }


        public void ORDER_MAN()
        {

            if (PickHeadDW.TO_HOST_DATE.HasValue)
            {
                // This is an EDI order, see ORDER_EDI
            }
            else
            {
                CreateTransactionDetailPreProcess(20, "Each", 1);
            }
        }

        public void ORDER_EDI()
        {

            if (PickHeadDW.TO_HOST_DATE.HasValue)
            {
                // This is an EDI order, 
                CreateTransactionDetailPreProcess(19, "Each", 1);
            }
            else
            {
                // Manual order - see ORDER_MAN
            }
        }

        public void ORDER_PRIORITY()
        {
            if (Calculated_IsPriority)
            {
                CreateTransactionDetailPreProcess(21, "Each", 1);
            }
        }

        public void INVENCL()
        {
            if (ChargeInvoiceEnclosed)
            {
                CreateTransactionDetailPreProcess(38, "Each", 1);
            }
        }
        public void PAL_DELAY()
        {
            if (PalletHireDelayDays > 0 && PalletCountFromPickHead > 0)
            {
                ChargeDetails cd = GetRateCollectionRateCodeIDRateChargeCodeDescription(37);

                StringBuilder FullDescription = new StringBuilder();
                FullDescription.Append("Pallet Hire Delay - " + PalletCountFromPickHead.ToString() + " pallet");
                if (PalletCountFromPickHead > 1)
                    FullDescription.Append("s");

                FullDescription.Append(" " + PalletHireDelayDays.ToString());

                if (PalletHireDelayDays > 1)
                    FullDescription.Append(" days");
                else
                    FullDescription.Append(" day");

                CreateTransactionDetail(cd.RateChargeCodeID, cd.RateChargeCode, "Per Pallet Per Day", FullDescription.ToString(), PalletHireDelayDays * PalletCountFromPickHead);
            }
        }

        public void ORD_PICK()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_Pick)
                {
                    CreateTransactionDetailPreProcess(arc_stat.ActionChargeCode, "Each", arc_stat.Quantity);
                }
            }
        }

        public void WRAPOUT_PAL()
        {
            if (WrapOut_Pal > 0)
            {
                CreateTransactionDetailPreProcess(26, "Each", WrapOut_Pal);
            }
        }

        public void ORD_CAPOUT_PAL()
        {
            // Caps are same logic as pallet wraps but some owners may not have liners; so this is separate
            if (WrapOut_Pal > 0)
            {
                CreateTransactionDetailPreProcess(52, "Each", WrapOut_Pal);
            }
        }
        public void ORD_LINEROUT_PAL()
        {
            // Liners are same logic as pallet wraps but some owners may not have liners; so this is separate
            if (WrapOut_Pal > 0)
            {
                CreateTransactionDetailPreProcess(53, "Each", WrapOut_Pal);
            }
        }


        public void LABELOUT()
        {
            if (ConnoteLabelCount > 0)
            {
                CreateTransactionDetailPreProcess(35, "Each", ConnoteLabelCount);
            }
        }

        public void HANDLE_OUT()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_HandleOut)
                {
                    CreateTransactionDetailPreProcess(arc_stat.ActionChargeCode, "Each", arc_stat.Quantity);
                }
            }
        }

        public void LOAD_OUT()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_LoadOut)
                {
                    CreateTransactionDetailPreProcess(arc_stat.ActionChargeCode, "Each", arc_stat.Quantity);
                }
            }
        }


        public void CONNOTE()
        {
            // order of charges to consider
            // 1. The Connote Fee row that matches the customer address ID and carrier
            // 2. The Connote Fee row that matches the customer address ID only (carrier 0)
            // 3. The Customer Address that matches the customer
            // 4. The generic Connote Fee row that matches the Carrier, then site, then owner
            // 5. The generic Connote Fee row with no Carrier that matches site, then owner

            // also important to note that if say 1 or 2 have a NOCHARGE there they will still be picked and then later on that charge will
            // be deleted/not contribute to the cost. Any found charges that are empty will be skipped over as if nothing was found
            // Connote fee for location on carrier is most specific, then going through 1-5 only the site and owner matching is least specific
            // while still being application
            // if 1 doesn't exist, 2 has '', 3 has NOCHARGE and 4 has CONNOTE. We find 2, then move to 3 and use that. it will just not produce a charge
            // if 3 was empty we would continue to 4 and use that.

            // internal priority order
            string RateCollectionRateChargeCode = "";

            List<ConnoteFee.dbRow> completecustomerCFmatch = ListConnoteFee.Where(x => CustomerAddress != null && x.CustomerAddressID == CustomerAddress.ID
                    && (x.CarrierID == 0 || x.CarrierID == CarrierID)).ToList();
            // Here if we get one result, ordering a list of 1 does nothing and we take that one result.
            // If there's two, one that has carrier wildcard and one that matches the order desc will mean match is first in the list and we take that
            if (completecustomerCFmatch.Count() > 0)
            {
                // so take matching carrier first
                RateCollectionRateChargeCode = completecustomerCFmatch.OrderByDescending(x => x.CarrierID).First().RateCollectionRateChargeCode;
            }
            if (RateCollectionRateChargeCode == "")
            {
                if (CustomerAddress != null && CustomerAddress.Connote_RateCollectionRateChargeCode != "")
                    RateCollectionRateChargeCode = CustomerAddress.Connote_RateCollectionRateChargeCode;
                if (RateCollectionRateChargeCode == "")
                {
                    // generic matches
                    List<ConnoteFee.dbRow> genericCFmatches = ListConnoteFee.Where(x => x.CustomerAddressID == 0 
                        && (x.CarrierID == 0 || x.CarrierID == CarrierID)
                        && (x.CompanyID == 0 || x.CompanyID == CompanyID)
                        && (x.OwnerID == 0 || x.OwnerID == OwnerID)
                        && (x.SiteID == 0 || x.SiteID == SiteID)).ToList();

                    if (genericCFmatches.Count() > 0)
                    {
                        // If there is no match on carrierID - we then look for rows where carrierID = 0 and in order:
                        // here a set of (Carrier = no match, Site = match), (Carrier = no match, Site = no match), (Carrier = match, Site = match)
                        // will pick double match at front of list
                        ConnoteFee.dbRow restCFmatch = genericCFmatches.OrderByDescending(x => x.CarrierID)
                            .ThenByDescending(x => x.SiteID).ThenByDescending(x => x.OwnerID).First();
                        RateCollectionRateChargeCode = restCFmatch.RateCollectionRateChargeCode;
                    }
                }
            }
            if (RateCollectionRateChargeCode != "")
                CreateTransactionDetailPreProcess(RateCollectionRateChargeCode, "Each", 1);

        }

        public string failedErrorMessage = "";
        public bool ProcessOrder()
        {
            if (TransactionHolder == null || TransactionHolder.TransactionHeader_Row == null)
            {
                TransactionHolder = new TransactionHolder();
                TransactionHolder.TransactionHeader_Row = new TransactionHeader.dbRow();
            CreateTransactionHeader();
            }
            else
            {
                TransactionHolder.TransactionHeader_Row.InvoiceExFuel = 0;
                TransactionHolder.TransactionHeader_Row.InvoiceExGST = 0;
                TransactionHolder.TransactionHeader_Row.InvoiceFuel = 0;
                TransactionHolder.TransactionHeader_Row.InvoiceGST = 0;
            }
            if (TransactionHolder.ListTransactionDetail == null)
                TransactionHolder.ListTransactionDetail = new List<TransactionDetail.dbRow>();
            if (TransactionHolder.PaperlessTransactionXref_Row == null)
                CreatePaperlessTransactionXRef();
            PreProcessData();
            if(!Process_Pick())
                return false;
            if (!Process_HandleOut())
                return false;

            if (PickHeadDW == null)
            {
                failedErrorMessage = "OrderDataHolder Process Order: No PickHeads to work with";
                logger.Error(failedErrorMessage);
                return false;
            }

            foreach (ProcessingRule.dbRow pr in ListProcessingRule)
            {
                switch (pr.ProcessID)
                {
                    case 15:
                        // ORD_MAN
                        ORDER_MAN();
                        break;
                    case 16:
                        // ORD_EDI
                        ORDER_EDI();
                        break;
                    case 17:
                        // ORD_PRIORITY
                        ORDER_PRIORITY();
                        break;
                    case 18:
                        // ORD_INV_ENCL
                        INVENCL();
                        break;
                    case 19:
                        // ORD_PICK
                        ORD_PICK();
                        break;
                    case 20:
                        // ORD_WRAPOUT_PAL
                        WRAPOUT_PAL();
                        break;
                    case 21:
                        // ORD_CONLABEL
                        LABELOUT();
                        break;
                    case 22:
                        // ORD_HANDLEOUT
                        HANDLE_OUT();
                        break;
                    case 23:
                        // ORD_LOADOUT
                        LOAD_OUT();
                        break;
                    case 24:
                        // ORD_CONNOTE
                        CONNOTE();
                        break;
                    case 25:
                        // PALHIRE_DELAY
                        PAL_DELAY();
                        break;
                    case 26:
                        // MINORDER
                        break;
                    case 35:
                        // CAP OUT
                        ORD_CAPOUT_PAL();
                        break;
                    case 36:
                        // LINER OUT
                        ORD_LINEROUT_PAL();
                        break;
                    default:
                        // should not be here...
                        break;
                }
            }

            if (addDebug)
                AddDebugInfo();
            TransactionHolder.TransactionHeader_Row.LastAmendedMethod = debugString.ToString();
            return true;
        }

        public bool addDebug = false;

        public void AddDebugInfo()
        {
            debugString.Append("Carrier: " + PickHeadDW.CARRIER_CODE + " - " + CarrierID.ToString() + ", ");
            debugString.Append("Zone: " + PickHeadDW.OUT_ZONE + " ");
            if (ZoneProcessing != null)
            {
                // pretend named order consolidation
                if (ZoneProcessing.IsCartonConsolidation)
                    debugString.Append("CC, ");
                else
                    debugString.Append("Not CC, ");
            }
            else
            {
                debugString.Append("ZP null, ");
            }

            debugString.Append("Load: " + PickHeadDW.LOAD_NO + ", ");

            if (ListTruckLoadDW != null)
            {
                debugString.Append("Count T_L " + ListTruckLoadDW.Count + ", ");

                int PalletCount = 0;
                List<string> trckldList = new List<string>();
                foreach (TRUCK_LOAD_DW tl_dw in ListTruckLoadDW)
                {
                    if (Int32.TryParse(tl_dw.RF_PAL_TYPE_COUNTED, out int paltype_counted))
                        PalletCount += paltype_counted;
                    trckldList.Add("TrckLd_Flg " + (tl_dw.RF_PAL_TYPE_ENTERED == "" ? "<empty>" : tl_dw.RF_PAL_TYPE_ENTERED) + ", ");
                }
                trckldList.GroupBy(x => x).Select((x, cnt) => debugString.Append(x + (cnt > 1 ? "(x" + cnt.ToString() + ")" : "")));
                debugString.Append("Count TrckLd_Tot " + PalletCount.ToString() + ", ");
            }
            else
            {
                debugString.Append("Count T_L: nulli, ");
            }

            if (ListPickDetailDW != null)
            {
                int CountPWD = Convert.ToInt32(ListPickDetailDW.Sum(r => r.QTY_PICKED));

                if (CountPWD != Pick_TotalCartonCount)
                {
                    debugString.Append("Mismatch Det: " + CountPWD.ToString() + " - SM: " + Pick_TotalCartonCount + ", ");
                }
            }

            if (CustomerAddress != null)
                debugString.Append("CustAdd: " + CustomerAddress.Name + ", ");

            if (CADUsed != null)
                debugString.Append("CustAddD: " + CADUsed.PublicID + ", ");
        }
        public void CreateTransactionHeader()
        {
            TransactionHolder.TransactionHeader_Row.CompanyID = CompanyID;
            TransactionHolder.TransactionHeader_Row.OwnerID = OwnerID;
            TransactionHolder.TransactionHeader_Row.SiteID = SiteID;
            TransactionHolder.TransactionHeader_Row.TransactionSourceID = 1; // Paperless
            TransactionHolder.TransactionHeader_Row.TransactionDateTime = Convert.ToDateTime(PickHeadDW.DATE_RECV);
            TransactionHolder.TransactionHeader_Row.TransactionTypeID = 1; // Order
            TransactionHolder.TransactionHeader_Row.TransactionReference = PaperlessOrderNumber;
            TransactionHolder.TransactionHeader_Row.Description = Owner_SourceOwnerID;
            TransactionHolder.TransactionHeader_Row.Description = PickHeadDW.CUST_NAME + ", " + PickHeadDW.CUST_SUB;
            TransactionHolder.TransactionHeader_Row.InvoiceHeaderID = 0;
            TransactionHolder.TransactionHeader_Row.CreatedDateTime = DateTime.Now;
            TransactionHolder.TransactionHeader_Row.CreatedMethod = "WBM_BGProcessor";
            TransactionHolder.TransactionHeader_Row.CreatedUserID = GS.selfUserID;
            TransactionHolder.TransactionHeader_Row.LastAmendedDateTime = TransactionHolder.TransactionHeader_Row.CreatedDateTime;
            TransactionHolder.TransactionHeader_Row.LastAmendedMethod = "";
            TransactionHolder.TransactionHeader_Row.LastAmendedUserID = TransactionHolder.TransactionHeader_Row.CreatedUserID;
            TransactionHolder.TransactionHeader_Row.CanInvoice = true;
        }

        public void CreatePaperlessTransactionXRef()
        {
            TransactionHolder.PaperlessTransactionXref_Row = new PaperlessTransactionXref.dbRow();
            TransactionHolder.PaperlessTransactionXref_Row.CompanyID = CompanyID;
            TransactionHolder.PaperlessTransactionXref_Row.PaperlessKeyID = PaperlessOrderNumber;
            TransactionHolder.PaperlessTransactionXref_Row.PaperlessDataTypeID = 1; // Order, 2 is Purchase Order
            TransactionHolder.PaperlessTransactionXref_Row.ReferenceNumber = PaperlessOrderNumber; // one of these should change
            TransactionHolder.PaperlessTransactionXref_Row.TransactionHeaderID = TransactionHolder.TransactionHeader_Row.ID;
            TransactionHolder.PaperlessTransactionXref_Row.CreatedDateTime = TransactionHolder.TransactionHeader_Row.CreatedDateTime;
            TransactionHolder.PaperlessTransactionXref_Row.CreatedMethod = TransactionHolder.TransactionHeader_Row.CreatedMethod;
            TransactionHolder.PaperlessTransactionXref_Row.CreatedUserID = TransactionHolder.TransactionHeader_Row.CreatedUserID;
            TransactionHolder.PaperlessTransactionXref_Row.LastAmendedDateTime = TransactionHolder.TransactionHeader_Row.LastAmendedDateTime;
            TransactionHolder.PaperlessTransactionXref_Row.LastAmendedMethod = TransactionHolder.TransactionHeader_Row.LastAmendedMethod;
            TransactionHolder.PaperlessTransactionXref_Row.LastAmendedUserID = TransactionHolder.TransactionHeader_Row.LastAmendedUserID;
        }

        public void CreateTransactionDetailPreProcess(int RateFunctionID, string ChargeBy, int Quantity)
        {
            ChargeDetails cd = GetRateCollectionRateCodeIDRateChargeCodeDescription(RateFunctionID);

            CreateTransactionDetail(cd.RateChargeCodeID, cd.RateChargeCode, ChargeBy, cd.Desciption, Quantity);
        }

        public void CreateTransactionDetailPreProcess(string RateChargeCode, string ChargeBy, int Quantity)
        {
            if (RateChargeCode != "NOCHARGE")
            {
                ChargeDetails cd = GetRateCollectionRateCodeIDRateChargeCodeDescription(0, RateChargeCode);

                CreateTransactionDetail(cd.RateChargeCodeID, cd.RateChargeCode, ChargeBy, cd.Desciption, Quantity);
            }
        }


        public void CreateTransactionDetail(int RateCodeID, string RateChargeCode, string ChargeBy, string FullDescription,
            int Quantity)
        {
            if (RateChargeCode != "NOCHARGE")
            {
                TransactionDetail.dbRow tdRow = new TransactionDetail.dbRow();
                tdRow.RateCode = RateChargeCode;
                TransactionDetail.dbRow tdRowExist = TransactionHolder.ListTransactionDetail.FirstOrDefault(x => x.RateCode == tdRow.RateCode);
                if (tdRowExist != null)
                    tdRow = tdRowExist;
                else
                {
                tdRow.CompanyID = TransactionHolder.TransactionHeader_Row.CompanyID;
                tdRow.OwnerID = TransactionHolder.TransactionHeader_Row.OwnerID;
                tdRow.SiteID = TransactionHolder.TransactionHeader_Row.SiteID;
                    tdRow.TransactionHeaderID = 0;
                tdRow.CreatedDateTime = TransactionHolder.TransactionHeader_Row.CreatedDateTime;
                tdRow.CreatedMethod = TransactionHolder.TransactionHeader_Row.CreatedMethod;
                tdRow.CreatedUserID = TransactionHolder.TransactionHeader_Row.CreatedUserID;
                tdRow.LastAmendedDateTime = TransactionHolder.TransactionHeader_Row.LastAmendedDateTime;
                tdRow.LastAmendedMethod = TransactionHolder.TransactionHeader_Row.LastAmendedMethod;
                tdRow.LastAmendedUserID = TransactionHolder.TransactionHeader_Row.LastAmendedUserID;

                TransactionHolder.ListTransactionDetail.Add(tdRow);
            }
                tdRow.TransactionDateTime = TransactionHolder.TransactionHeader_Row.TransactionDateTime;
                tdRow.RateCodeID = RateCodeID;
                tdRow.ChargeDescription = ChargeBy;
                tdRow.FullDescription = FullDescription;
                tdRow.Quantity = Quantity;
        }
        }

        public bool Process_Pick()
        {
            Stock.dbRow stock_row = null;
            Decimal qty = 0;

            int this_unitTypeID = 0;
            Boolean IsPick = false;

            foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
            {
                #region Calculate Pick charges

                IsPick = false;


                switch (sm.MOVE_TYPE)
                {
                    case "FULL.PICK":
                        Pick_FullPalletCount++;
                        this_unitTypeID = UnitTypeID_Pallet;
                        IsPick = true;
                        break;
                    case "PART.PCK":
                        this_unitTypeID = UnitTypeID_Carton;
                        Pick_CartonCount += sm.MOVE_QTY.HasValue ? sm.MOVE_QTY.Value : 0;
                        IsPick = true;
                        break;
                    default:
                        // Is not a pick
                        break;
                }

                if (IsPick)
                {
                    Pick_TotalCartonCount += sm.MOVE_QTY.HasValue ? sm.MOVE_QTY.Value : 0;

                    stock_row = GetStockRow(sm.PRODUCT_CODE);
                    // need to check if it is a carton pick with an exact multiple of a pick layer since then we charge layer
                    if (this_unitTypeID == UnitTypeID_Carton && sm.MOVE_QTY.HasValue && sm.MOVE_QTY.Value > 0 && stock_row.LayerPickEligible && sm.MOVE_QTY.Value % stock_row.ItemsPerLayer == 0)
                        this_unitTypeID = UnitTypeID_Layer;
                    // Count MOVE.QTY grouped on ActionChargeCode
                    if (stock_row != null)
                    {
                        string ActionChargeCode = GetActionChargeCode(stock_row, ActionTypeID_Pick, this_unitTypeID);

                        if (ActionChargeCode != null)
                        {
                            ActionRateCodeStat arc_stat = null;
                            if (DictActionRateCodeStat.ContainsKey(ActionChargeCode))
                            {
                                arc_stat = DictActionRateCodeStat[ActionChargeCode];
                            }
                            else
                            {
                                arc_stat = new ActionRateCodeStat() { ActionTypeID = ActionTypeID_Pick, UnitTypeID = this_unitTypeID, ActionChargeCode = ActionChargeCode };
                                DictActionRateCodeStat.Add(ActionChargeCode, arc_stat);
                            }
                            if (this_unitTypeID == UnitTypeID_Carton)
                            {
                                qty = sm.MOVE_QTY.HasValue ? sm.MOVE_QTY.Value : 0;
                                arc_stat.Quantity += Convert.ToInt32(qty);
                            }
                            else if (this_unitTypeID == UnitTypeID_Pallet)
                                arc_stat.Quantity += 1;
                            else if (this_unitTypeID == UnitTypeID_Layer)
                                arc_stat.Quantity += (int)sm.MOVE_QTY.Value / stock_row.ItemsPerLayer;
                            }
                        else
                        {
                            failedErrorMessage = "OrderDataHolder Process Pick: No Action Charge Code was found for pick stock row: " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + PaperlessOrderNumber;
                            logger.Error(failedErrorMessage);
                            return false;
                        }
                    }
                    else
                    {
                        failedErrorMessage = "OrderDataHolder Process Pick: No pick stock row was found: " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + PaperlessOrderNumber;
                        logger.Error(failedErrorMessage);
                        return false;
                    }
                }
                #endregion
            }
            return true;
        }

        public bool Process_HandleOut()
        {
            #region Check that total rows and pallets are what we expect
            PalletCountFromTruckLoad = 0;
            bool rfPalTypeIsN = ListTruckLoadDW.Any(x => x.RF_PAL_TYPE_ENTERED != "" && x.RF_PAL_TYPE_ENTERED.Trim() == "N");

            int paltype_counted = 0;
            foreach (TRUCK_LOAD_DW tl_dw in ListTruckLoadDW)
            {
                if (!rfPalTypeIsN && Int32.TryParse(tl_dw.RF_PAL_TYPE_COUNTED, out paltype_counted))
                    PalletCountFromTruckLoad += paltype_counted;
            }
            #endregion

            if (PickHeadDW.PAL_TYPE_ENTERED != "" && PickHeadDW.PAL_TYPE_ENTERED.Trim() != "N")
            {
                // PAL_TYPE_COUNTED is a string?; whilst supposed to contain int can contain decimal eg 11.7; We'll take the int part.
                if (Decimal.TryParse(PickHeadDW.PAL_TYPE_COUNTED, out Decimal tempDec))
                {
                    PalletCountFromPickHead = (int)tempDec;
                }
            }

            #region Wrap out
            // pretend named order consolidation
            if (ZoneProcessing != null && !ZoneProcessing.IsCartonConsolidation)
            {
                if (PalletCountFromPickHead > Pick_FullPalletCount)
                {
                    // We must have consolidated some cartons onto pallets and
                    // wrapped; charge a wrap out for the difference
                    WrapOut_Pal = PalletCountFromPickHead - Pick_FullPalletCount;
                }
                else
                {
                    if (PalletCountFromPickHead == Pick_FullPalletCount && PalletCountFromPickHead > 0 && Pick_CartonCount > 0)
                    {
                        // We will assume there is 1 wrap, there has to be a pallet going out on the truck though, otherwise no wrap occurred
                        WrapOut_Pal = 1;
                    }
                }
            }
            else
                WrapOut_Pal = 0;
            #endregion

            #region Connote label charge

            if (ZoneProcessing != null)
            {
                // pretend named order consolidation
                if (ZoneProcessing.IsCartonConsolidation)
                {
                    // cartons are individually labeled
                    ConnoteLabelCount = Convert.ToInt32(Pick_TotalCartonCount);
                }
                else
                {
                    // Assume this is what we loaded out
                    if (PalletCountFromPickHead > 0)
                        ConnoteLabelCount = PalletCountFromPickHead;
                    else
                        ConnoteLabelCount = Convert.ToInt32(Pick_TotalCartonCount);
                }
            }
            else
            {
                // Assume this is what we loaded out
                if (PalletCountFromPickHead > 0)
                    ConnoteLabelCount = PalletCountFromPickHead;
                else
                    ConnoteLabelCount = Convert.ToInt32(Pick_TotalCartonCount);
            }
            #endregion

            return CalculateHandleOut();
        }

        private bool CalculateHandleOut()
        {
            if (ZoneProcessing != null)
            {
                // pretend named order consolidation
                if (ZoneProcessing.IsCartonConsolidation)
                {
                    // if carton consolidation don't generate load out charges
                }
                else
                {
                    #region Non-consolidation
                    // Not a carton consolidation zone; so full pallets are counted as 1, 
                    // cartons handled individually

                    Stock.dbRow stock_row = null;
                    Decimal qty = 0;
                    Boolean IsLoadOut = false;

                    int this_unitTypeID = 0;
                    if (PalletCountFromPickHead > 0)
                    {
                        this_unitTypeID = UnitTypeID_Pallet;
                        // get the action rate code of a default stock category and 0 dimensions
                        stock_row = new Stock.dbRow
                        {
                            StockRateCategoryID = 0,
                            Length = 0,
                            Width = 0
                        };
                        string ActionChargeCode = GetActionChargeCode(stock_row, ActionTypeID_LoadOut, UnitTypeID_Pallet);
                        ActionRateCodeStat arc_stat = null;
                        if (DictActionRateCodeStat.ContainsKey(ActionChargeCode))
                        {
                            arc_stat = DictActionRateCodeStat[ActionChargeCode];
                        }
                        else
                        {
                            arc_stat = new ActionRateCodeStat() { ActionTypeID = ActionTypeID_LoadOut, UnitTypeID = this_unitTypeID, ActionChargeCode = ActionChargeCode };
                            DictActionRateCodeStat.Add(ActionChargeCode, arc_stat);
                        }
                        arc_stat.Quantity += PalletCountFromPickHead;
                    }
                    else
                    {
                        this_unitTypeID = UnitTypeID_Carton;
                        foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
                        {
                            #region Calculate Handle out charges

                            IsLoadOut = false;

                            switch (sm.MOVE_TYPE)
                            {
                                case "FULL.PICK":
                                    IsLoadOut = true;
                                    break;
                                case "PART.PCK":
                                    IsLoadOut = true;
                                    break;
                                default:
                                    // Is not a pick
                                    break;
                            }

                            if (IsLoadOut)
                            {
                                stock_row = GetStockRow(sm.PRODUCT_CODE);
                                // need to check if it is a carton pick with an exact multiple of a pick layer since then we charge layer
                                if (sm.MOVE_QTY.HasValue && sm.MOVE_QTY.Value > 0 && stock_row.LayerPickEligible && sm.MOVE_QTY.Value % stock_row.ItemsPerLayer == 0)
                                    this_unitTypeID = UnitTypeID_Layer;
                                else
                                this_unitTypeID = UnitTypeID_Carton;

                                // Count MOVE.QTY grouped on ActionChargeCode
                                if (stock_row != null)
                                {
                                    string ActionChargeCode = GetActionChargeCode(stock_row, ActionTypeID_LoadOut, this_unitTypeID);

                                    if (ActionChargeCode != null)
                                    {
                                        ActionRateCodeStat arc_stat = null;
                                        if (DictActionRateCodeStat.ContainsKey(ActionChargeCode))
                                        {
                                            arc_stat = DictActionRateCodeStat[ActionChargeCode];
                                        }
                                        else
                                        {
                                            arc_stat = new ActionRateCodeStat() { ActionTypeID = ActionTypeID_LoadOut, UnitTypeID = this_unitTypeID, ActionChargeCode = ActionChargeCode };
                                            DictActionRateCodeStat.Add(ActionChargeCode, arc_stat);
                                        }
                                        qty = sm.MOVE_QTY.HasValue ? sm.MOVE_QTY.Value : 0;
                                        arc_stat.Quantity += Convert.ToInt32(qty);
                                    }
                                    else
                                    {
                                        failedErrorMessage = "OrderDataHolder Calculate Handle Out Zone Processing: No Action Charge Code was found for load out stock row: " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + PaperlessOrderNumber;
                                        logger.Error(failedErrorMessage);
                                        return false;
                                    }
                                }
                                else
                                {
                                    failedErrorMessage = "OrderDataHolder Calculate Handle Out Zone Processing: No load out stock row was found: " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + PaperlessOrderNumber;
                                    logger.Error(failedErrorMessage);
                                    return false;
                                }
                            }
                            #endregion
                        }
                    }
                    #endregion
                }
            }
            else
            {
                // Assume this is what we loaded out
                #region Non-consolidation
                // Not a carton consolidation zone; so full pallets are counted as 1, 
                // cartons handled individually

                Stock.dbRow stock_row = null;
                Decimal qty = 0;
                Boolean IsLoadOut = false;

                int this_unitTypeID = 0;
                foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
                {
                    #region Calculate Handle out charges

                    IsLoadOut = false;

                    switch (sm.MOVE_TYPE)
                    {
                        case "FULL.PICK":
                            IsLoadOut = true;
                            this_unitTypeID = UnitTypeID_Pallet;
                            //PalletCountFromTruckLoad++;
                            break;
                        case "PART.PCK":
                            IsLoadOut = true;
                            this_unitTypeID = UnitTypeID_Carton;
                            break;
                        default:
                            // Is not a pick
                            break;
                    }

                    if (IsLoadOut)
                    {
                        stock_row = GetStockRow(sm.PRODUCT_CODE);

                        // Count MOVE.QTY grouped on ActionChargeCode
                        if (stock_row != null)
                        {
                            // need to check if it is a carton pick with an exact multiple of a pick layer since then we charge layer
                            if (this_unitTypeID == UnitTypeID_Carton && sm.MOVE_QTY.HasValue && sm.MOVE_QTY.Value > 0 && stock_row.LayerPickEligible && sm.MOVE_QTY.Value % stock_row.ItemsPerLayer == 0)
                                this_unitTypeID = UnitTypeID_Layer;
                            string ActionChargeCode = GetActionChargeCode(stock_row, ActionTypeID_LoadOut, this_unitTypeID);

                            if (ActionChargeCode != null)
                            {
                                ActionRateCodeStat arc_stat = null;
                                if (DictActionRateCodeStat.ContainsKey(ActionChargeCode))
                                {
                                    arc_stat = DictActionRateCodeStat[ActionChargeCode];
                                }
                                else
                                {
                                    arc_stat = new ActionRateCodeStat() { ActionTypeID = ActionTypeID_LoadOut, UnitTypeID = this_unitTypeID, ActionChargeCode = ActionChargeCode };
                                    DictActionRateCodeStat.Add(ActionChargeCode, arc_stat);
                                }
                                if (this_unitTypeID == UnitTypeID_Carton)
                                {
                                    qty = sm.MOVE_QTY.HasValue ? sm.MOVE_QTY.Value : 0;
                                    arc_stat.Quantity += Convert.ToInt32(qty);
                                }
                                else if (this_unitTypeID == UnitTypeID_Pallet)
                                    arc_stat.Quantity += 1;
                                else if (this_unitTypeID == UnitTypeID_Layer)
                                    arc_stat.Quantity += (int) sm.MOVE_QTY.Value / stock_row.ItemsPerLayer;
                            }
                            else
                            {
                                failedErrorMessage = "OrderDataHolder Calculate Handle Out: No Action Charge Code was found for load out stock row: " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + PaperlessOrderNumber;
                                logger.Error(failedErrorMessage);
                                return false;
                            }
                        }
                        else
                        {
                            failedErrorMessage = "OrderDataHolder Calculate Handle Out: No load out stock row was found: " + sm.PRODUCT_CODE + ", " + sm.MOVE_TYPE + ", " + PaperlessOrderNumber;
                            logger.Error(failedErrorMessage);
                            return false;
                        }
                    }
                    else
                    {
                        failedErrorMessage = "OrderDataHolder Calculate Handle Out: stock move type was not a load out: " + sm.MOVE_TYPE + ", " + PaperlessOrderNumber;
                        logger.Error(failedErrorMessage);
                        return false;
                    }
                    #endregion
                }
                #endregion
            }
            return true;
        }

        #endregion

        private Stock.dbRow GetStockRow(string Product_Code)
        {
            Stock.dbRow found_row = null;
            if (ListStock != null)
            {
                foreach (Stock.dbRow s in ListStock)
                {
                    if (s.ProductCode == Product_Code)
                    {
                        found_row = s;
                        break;
                    }
                }
            }
            return found_row;
        }

        private string GetActionChargeCode(Stock.dbRow stock_row, int ActionTypeID, int UnitTypeID)
        {
            List<ActionRateCode.dbRow> ListPotentialMatches = new List<ActionRateCode.dbRow>();

            string ActionChargeCode = null;
            if (stock_row != null)
            {
                ActionRateCode.dbRow found_row = null;
                Boolean DisqualifyRule = false;
                foreach (ActionRateCode.dbRow arc in ListActionRateCode)
                {
                    // Non negotiable rules
                    if (arc.ActionTypeID != ActionTypeID)
                        continue;

                    if (arc.UnitTypeID != UnitTypeID)
                        continue;

                    if (arc.IsPriority.HasValue && arc.IsPriority.Value != Calculated_IsPriority)
                        continue;

                    if (arc.SiteID > 0 && arc.SiteID != SiteID)
                        continue;

                    //--

                    if (arc.StockRateCategoryID > 0 && arc.StockRateCategoryID != stock_row.StockRateCategoryID)
                        continue;

                    if (arc.LengthBreak > 0 && arc.LengthBreak < stock_row.Length)
                        continue;

                    if (arc.WeightBreak > 0 && arc.WeightBreak < stock_row.Weight)
                        continue;

                    ListPotentialMatches.Add(arc);
                }

                if (ListPotentialMatches.Count > 0)
                {
                    if (ListPotentialMatches.Count == 1)
                        found_row = ListPotentialMatches[0];
                    else
                    {
                        found_row = ChooseActionChargeCode(ListPotentialMatches);
                    }
                }
                if (found_row != null)
                {
                    ActionChargeCode = found_row.RateCollectionRateChargeCode;
                }
            }
            return ActionChargeCode;
        }

        private ActionRateCode.dbRow ChooseActionChargeCode(List<ActionRateCode.dbRow> ListPotentialMatches)
        {
            ActionRateCode.dbRow found_row = null;

            if (ListPotentialMatches != null)
            {
                // Heirachy
                found_row = ListPotentialMatches.Where(arc => arc.StockRateCategoryID > 0 && arc.SiteID > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.StockRateCategoryID > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0 && arc.LengthBreak > 0 && arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0 && arc.LengthBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0 && arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.LengthBreak > 0 && arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.LengthBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.WeightBreak > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                found_row = ListPotentialMatches.Where(arc => arc.SiteID > 0).FirstOrDefault();
                if (found_row != null)
                    return found_row;

                // If we're here then just return the first row
                return ListPotentialMatches[0];
            }
            else
                return null;

        }

        private ChargeDetails GetRateCollectionRateCodeIDRateChargeCodeDescription(int RateFunctionID, string RateChargeCode = null)
        {
            RateCollection.dbRow rc;

            if (RateFunctionID > 0)
                rc = ListRateCollection.FirstOrDefault(r => r.RateFunctionID == RateFunctionID);
            else
                rc = ListRateCollection.FirstOrDefault(r => r.RateChargeCode == RateChargeCode);

            if (rc != null)
                return new ChargeDetails(rc.RateCodeID, rc.RateChargeCode, rc.Description);
            else
            {
                if (RateFunctionID > 0)
                    return new ChargeDetails(0, "Not Found: " + RateFunctionID.ToString(), "Not Found: " + RateFunctionID.ToString());
                else
                    return new ChargeDetails(0, "Not Found: " + RateChargeCode, "Not Found: " + RateChargeCode);

            }
        }
    }
}
