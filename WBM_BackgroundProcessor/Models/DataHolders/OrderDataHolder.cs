using NLog.Targets;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using System.Text;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using WBM_BackgroundProcessor.Polling;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Models.DataHolders
{
    public class OrderDataHolder
    {
        public OrderDataHolder(int CompanyID, int SiteID, int OwnerID)
        {
            TransactionHeader = new TransactionHeader.dbRow();
            ListTransactionDetail = new List<TransactionDetail.dbRow>();
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

        #endregion

        #region Warehouse Billing Module Datatables

        // Warehouse Billing Module Datatables

        public List<ActionRateCode.dbRow> ListActionRateCode { get; set; }

        public List<PriorityOrder.dbRow> ListPriorityOrder { get; set; }

        public List<CustomerAddressDefault.dbRow> ListCustomerAddressDefault { get; set; }

        public CustomerAddress.dbRow CustomerAddress { get; set; }

        public List<Stock.dbRow> ListStock { get; set; }

        public List<ConnoteFee.dbRow> ListConnoteFee { get; set; }

        public List<Carrier.dbRow> ListCarrier { get; set; }

        public TransactionHeader.dbRow TransactionHeader { get; set; }

        public List<TransactionDetail.dbRow> ListTransactionDetail { get; set; }

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
        public int TotalPalletOut { get; set; }

        public int ConnoteLabelCount { get; set; }
        public Boolean ChargeInvoiceEnclosed { get; set; }

        public int PalletHireDelayDays { get; set; }

        private int ActionTypeID_Pick = 1;
        private int ActionTypeID_HandleOut = 4;
        private int ActionTypeID_LoadOut = 5;
        private int UnitTypeID_Carton = 1;
        private int UnitTypeID_Pallet = 2;

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

            if (!GotInvEncl || GotPalletHireDelayDays)
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
                // This is an EDI order, no manual charge applied
            }
            else
            {
                CreateTransactionDetail(20, "", "Each", "Manual order", 1);
            }
        }

        public void ORDER_EDI()
        {

            if (PickHeadDW.TO_HOST_DATE.HasValue)
            {
                // This is an EDI order, 
                CreateTransactionDetail(19, "", "Each", "EDI Order", 1);
            }
            else
            {
                // Manual order
            }
        }

        public void ORDER_PRIORITY()
        {
            if (Calculated_IsPriority)
            {
                CreateTransactionDetail(21, "", "Each", "Priority order", 1);
            }
        }

        public void INVENCL()
        {
            if (ChargeInvoiceEnclosed)
            {
                CreateTransactionDetail(38, "", "Each", "Invoice Enclosed Envelope", 1);
            }
        }
        public void PAL_DELAY()
        {
            if (PalletHireDelayDays > 0 && TotalPalletOut > 0)
            {
                StringBuilder FullDescription = new StringBuilder();
                FullDescription.Append("Pallet Hire Delay - " + TotalPalletOut.ToString() + " pallet");
                if (TotalPalletOut > 1)
                    FullDescription.Append("s");

                FullDescription.Append(PalletHireDelayDays.ToString());

                if (PalletHireDelayDays > 1)
                    FullDescription.Append(" days");
                else
                    FullDescription.Append(" day");

                CreateTransactionDetail(37, "", "Per Pal Per Day", FullDescription.ToString(), PalletHireDelayDays);
            }
        }


        public void ORD_PICK()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_Pick)
                {
                    string ChargeDescription = "Each";
                    string FullDescription = "Pick carton";

                    if (arc_stat.UnitTypeID == 2)
                        FullDescription = "Pick full pallet";

                    CreateTransactionDetail(0, arc_stat.ActionChargeCode, ChargeDescription, FullDescription, arc_stat.Quantity);
                }
            }
        }

        public void WRAPOUT_PAL()
        {
            if (WrapOut_Pal > 0)
            {
                CreateTransactionDetail(26, "", "Each", "Wrap out pallet", WrapOut_Pal);
            }
        }

        public void LABELOUT()
        {
            if (ConnoteLabelCount > 0)
            {
                CreateTransactionDetail(35, "", "Each", "Outbound label", ConnoteLabelCount);
            }
        }

        public void HANDLE_OUT()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == ActionTypeID_HandleOut)
                {
                    string ChargeDescription = "Each";
                    string FullDescription = "Handle out carton";

                    if (arc_stat.UnitTypeID == 2)
                        FullDescription = "Handle out full pallet";

                    CreateTransactionDetail(0, arc_stat.ActionChargeCode, ChargeDescription, FullDescription, arc_stat.Quantity);
                }
            }
        }

        public void LOAD_OUT()
        {
            foreach (ActionRateCodeStat arc_stat in DictActionRateCodeStat.Values)
            {
                if (arc_stat.ActionTypeID == 5)
                {
                    string ChargeDescription = "Each";
                    string FullDescription = "Load out carton";

                    if (arc_stat.UnitTypeID == 2)
                        FullDescription = "Load out full pallet";

                    CreateTransactionDetail(0, arc_stat.ActionChargeCode, ChargeDescription, FullDescription, arc_stat.Quantity);
                }
            }
        }


        public void CONNOTE()
        {
            string ConnoteChargeCode = null;

            ConnoteFee.dbRow found_row = null;
            Boolean DisqualifyRule = false;
            foreach (ConnoteFee.dbRow cad in ListConnoteFee)
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

                if (!DisqualifyRule && (cad.CustomerNumber != "" && cad.CustomerNumber != PickHeadDW.CUST_NO))
                {
                    DisqualifyRule = true;
                }

                if (cad.CarrierID > 0 && cad.CarrierID != CarrierID)
                {
                    DisqualifyRule = true;
                }

                if (!DisqualifyRule)
                    found_row = cad;
            }
            if (found_row != null)
            {
                CreateTransactionDetail(0, found_row.RateCollectionRateChargeCode, "Each", "3rd Pty Connote", 1);

                ConnoteChargeCode = found_row.RateCollectionRateChargeCode;

            }

        }

        public void ProcessOrder()
        {
            CreateTransactionHeader();
            PreProcessData();
            Process_Pick();
            Process_HandleOut();

            if (PickHeadDW == null)
            {
                // Error, cannot proceed
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
                        break;
                    case 26:
                        // MINORDER
                        break;
                    default:
                        // should not be here...
                        break;
                }
            }

            AddDebugInfo();
            TransactionHeader.LastAmendedMethod = debugString.ToString();
        }


        public void AddDebugInfo()
        {
            debugString.Append("Carrier: " + PickHeadDW.CARRIER_CODE + " - " + CarrierID.ToString() + ", ");
            debugString.Append("Zone: " + PickHeadDW.OUT_ZONE + " ");
            if (ZoneProcessing != null)
            {
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
                foreach (TRUCK_LOAD_DW tl_dw in ListTruckLoadDW)
                {
                    if (Int32.TryParse(tl_dw.RF_PAL_TYPE_COUNTED, out int paltype_counted))
                        PalletCount += paltype_counted;
                    debugString.Append("TrckLd_Flg " + (tl_dw.RF_PAL_TYPE_ENTERED == "" ? "<empty>" : tl_dw.RF_PAL_TYPE_ENTERED) + ", ");
                }
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


        }
        public void CreateTransactionHeader()
        {
            TransactionHeader.CompanyID = CompanyID;
            TransactionHeader.OwnerID = OwnerID;
            TransactionHeader.SiteID = SiteID;
            TransactionHeader.TransactionSourceID = 1; // Paperless
            TransactionHeader.TransactionDateTime = Convert.ToDateTime(PickHeadDW.DATE_RECV);
            TransactionHeader.TransactionTypeID = 2; // Order
            TransactionHeader.TransactionReference = PaperlessOrderNumber;
            TransactionHeader.Description = "Order";
            TransactionHeader.InvoiceID = 0;
            TransactionHeader.InvoiceDateTime = DateTime.Now;
            TransactionHeader.CreatedDateTime = DateTime.Now;
            TransactionHeader.CreatedMethod = "WBM_BGProcessor";
            TransactionHeader.CreatedUserID = GS.selfUserID;
            TransactionHeader.LastAmendedDateTime = TransactionHeader.CreatedDateTime;
            TransactionHeader.LastAmendedMethod = "";
            TransactionHeader.LastAmendedUserID = TransactionHeader.CreatedUserID;

            TransactionHeader.CreatedMethod = PickHeadDW.KEYID;
        }

        public void CreateTransactionDetail(int RateFunctionID, string RateCode, string ChargeDescription, string FullDescription,
            int Quantity)
        {
            if (RateCode != "NOCHARGE")
            {
                TransactionDetail.dbRow tdRow = new TransactionDetail.dbRow();
                tdRow.TransactionHeaderID = 0;
                tdRow.CompanyID = TransactionHeader.CompanyID;
                tdRow.OwnerID = TransactionHeader.OwnerID;
                tdRow.SiteID = TransactionHeader.SiteID;
                tdRow.TransactionDateTime = TransactionHeader.TransactionDateTime;
                tdRow.RateCodeID = RateFunctionID;
                tdRow.RateCode = RateCode;
                tdRow.ChargeDescription = ChargeDescription;
                tdRow.FullDescription = FullDescription;
                tdRow.Quantity = Quantity;

                tdRow.CreatedDateTime = TransactionHeader.CreatedDateTime;
                tdRow.CreatedMethod = TransactionHeader.CreatedMethod;
                tdRow.CreatedUserID = TransactionHeader.CreatedUserID;
                tdRow.LastAmendedDateTime = TransactionHeader.LastAmendedDateTime;
                tdRow.LastAmendedMethod = TransactionHeader.LastAmendedMethod;
                tdRow.LastAmendedUserID = TransactionHeader.LastAmendedUserID;

                ListTransactionDetail.Add(tdRow);
            }

        }

        public void Process_Pick()
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
                            {
                                arc_stat.Quantity += 1;
                            }
                        }
                        else
                        {
                            // Error....
                        }
                    }
                }
                else
                {
                    // Error....
                }
                #endregion
            }

        }

        public void Process_HandleOut()
        {
            #region Check that total rows and pallets are what we expect
            PalletCountFromTruckLoad = 0;
            int RowCount = 0;
            bool rfPalTypeIsN = ListTruckLoadDW.Any(x => x.RF_PAL_TYPE_ENTERED != "" && x.RF_PAL_TYPE_ENTERED.Trim() == "N");

            int paltype_counted = 0;
            foreach (TRUCK_LOAD_DW tl_dw in ListTruckLoadDW)
            {
                RowCount++;
                if (!rfPalTypeIsN && Int32.TryParse(tl_dw.RF_PAL_TYPE_COUNTED, out paltype_counted))
                    PalletCountFromTruckLoad += paltype_counted;
            }

            if (RowCount != ListTruckLoadDW.Count)
            {
                // Error....
            }
            #endregion


            #region Wrap out
            if (!rfPalTypeIsN && ZoneProcessing != null && !ZoneProcessing.IsCartonConsolidation)
            {
                if (PalletCountFromTruckLoad > Pick_FullPalletCount)
                {
                    // We must have consolidated some cartons onto pallets and
                    // wrapped; charge a wrap out for the difference
                    WrapOut_Pal = PalletCountFromTruckLoad - Pick_FullPalletCount;
                }
                else
                {
                    if (PalletCountFromTruckLoad == Pick_FullPalletCount && PalletCountFromTruckLoad > 0 && Pick_CartonCount > 0)
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
                if (ZoneProcessing.IsCartonConsolidation)
                {
                    // cartons are individually labeled
                    ConnoteLabelCount = Convert.ToInt32(Pick_TotalCartonCount);
                }
                else
                {
                    // Assume this is what we loaded out
                    ConnoteLabelCount = PalletCountFromTruckLoad;
                }
            }
            else
            {
                // Assume this is what we loaded out
                ConnoteLabelCount = PalletCountFromTruckLoad;
            }


            #endregion

            CalculateHandleOut();

        }

        private void CalculateHandleOut()
        {
            if (ZoneProcessing != null)
            {
                if (ZoneProcessing.IsCartonConsolidation)
                {
                    #region Carton Consolidation
                    // Pallets are broken down into cartons and each carton is a handleout
                    Stock.dbRow stock_row = null;
                    int this_quantity = 0;
                    Boolean IsPick = false;

                    foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
                    {
                        #region Calculate Handle out charges

                        IsPick = false;

                        switch (sm.MOVE_TYPE)
                        {
                            case "FULL.PICK":
                                IsPick = true;
                                break;
                            case "PART.PCK":
                                IsPick = true;
                                break;
                            default:
                                // Is not a pick
                                break;
                        }

                        if (IsPick)
                        {
                            if (sm.MOVE_QTY.HasValue)
                                this_quantity = Convert.ToInt32(sm.MOVE_QTY.Value);
                            else
                                this_quantity = 0;

                            stock_row = GetStockRow(sm.PRODUCT_CODE);
                            // Count MOVE.QTY grouped on ActionChargeCode
                            if (stock_row != null && this_quantity > 0)
                            {
                                string ActionChargeCode = GetActionChargeCode(stock_row, ActionTypeID_LoadOut, UnitTypeID_Carton);

                                if (ActionChargeCode != null)
                                {
                                    ActionRateCodeStat arc_stat = null;
                                    if (DictActionRateCodeStat.ContainsKey(ActionChargeCode))
                                    {
                                        arc_stat = DictActionRateCodeStat[ActionChargeCode];
                                    }
                                    else
                                    {
                                        arc_stat = new ActionRateCodeStat() { ActionTypeID = ActionTypeID_LoadOut, UnitTypeID = UnitTypeID_Carton, ActionChargeCode = ActionChargeCode };
                                        DictActionRateCodeStat.Add(ActionChargeCode, arc_stat);
                                    }

                                    arc_stat.Quantity += this_quantity;
                                }
                                else
                                {
                                    // Error....
                                }
                            }
                            else
                            {
                                // Error....
                            }
                        }
                        #endregion
                    }
                    #endregion
                }
                else
                {
                    #region Non-consolidation
                    // Not a carton consolidation zone; so full pallets are counted as 1, 
                    // cartons handled individually

                    Stock.dbRow stock_row = null;
                    Decimal qty = 0;
                    Boolean IsPick = false;

                    int this_unitTypeID = 0;
                    if (PalletCountFromTruckLoad > 0)
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
                        arc_stat.Quantity += PalletCountFromTruckLoad;
                    }
                    else
                    {
                        this_unitTypeID = UnitTypeID_Carton;
                        foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
                        {
                            #region Calculate Handle out charges

                            IsPick = false;

                            switch (sm.MOVE_TYPE)
                            {
                                case "FULL.PICK":
                                    IsPick = true;
                                    break;
                                case "PART.PCK":
                                    IsPick = true;
                                    break;
                                default:
                                    // Is not a pick
                                    break;
                            }

                            if (IsPick)
                            {
                                stock_row = GetStockRow(sm.PRODUCT_CODE);
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
                                        // Error....
                                    }
                                }
                                else
                                {
                                    // Error....
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
                Boolean IsPick = false;

                int this_unitTypeID = 0;
                foreach (STOCK_MOVE_DW sm in ListStockMoveDW)
                {
                    #region Calculate Handle out charges

                    IsPick = false;

                    switch (sm.MOVE_TYPE)
                    {
                        case "FULL.PICK":
                            IsPick = true;
                            this_unitTypeID = UnitTypeID_Pallet;
                            break;
                        case "PART.PCK":
                            IsPick = true;
                            this_unitTypeID = UnitTypeID_Carton;
                            break;
                        default:
                            // Is not a pick
                            break;
                    }

                    if (IsPick)
                    {
                        stock_row = GetStockRow(sm.PRODUCT_CODE);

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
                                if (this_unitTypeID == UnitTypeID_Carton)
                                {
                                    qty = sm.MOVE_QTY.HasValue ? sm.MOVE_QTY.Value : 0;
                                    arc_stat.Quantity += Convert.ToInt32(qty);
                                }
                                else if (this_unitTypeID == UnitTypeID_Pallet)
                                {
                                    arc_stat.Quantity += 1;
                                }
                            }
                            else
                            {
                                // Error....
                            }
                        }
                        else
                        {
                            // Error....
                        }
                    }
                    #endregion
                }
                #endregion
            }

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
    }
}
