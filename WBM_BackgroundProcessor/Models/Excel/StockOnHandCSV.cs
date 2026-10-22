using CsvHelper.Configuration.Attributes;

namespace WBM_BackgroundProcessor.Models.Excel
{
    // One line of the stock on hand CSV (RP_<Site>_CSR2_..._<ddMMyyHHmmss>.csv).
    // Only the columns we save are mapped (Decsription is not), values are read as text and converted when saving.
    public class StockOnHandCsvRow
    {
        [Name("Product Code")]
        public string ProductCode { get; set; }

        [Name("Bin Location")]
        public string BinLocation { get; set; }

        [Name("Quantity")]
        public string Quantity { get; set; }

        [Name("Owner")]
        public string Owner { get; set; }

        [Name("Pallet Number")]
        public string PalletNumber { get; set; }

        [Name("Position Zone")]
        public string PositionZone { get; set; }

        [Name("Status")]
        public string Status { get; set; }

        [Name("Use-by Date")]
        public string UseByDate { get; set; }

        [Name("Lot Number")]
        public string LotNumber { get; set; }

        [Name("Pallet Weight")]
        public string PalletWeight { get; set; }

        [Name("Pallet Width")]
        public string PalletWidth { get; set; }

        [Name("Pallet Depth")]
        public string PalletDepth { get; set; }

        [Name("Pallet Height")]
        public string PalletHeight { get; set; }

        [Name("Pall.Type")]
        public string PalletType { get; set; }

        // Line in the file this row came from, filled in when reading, so log messages can point to it
        [Ignore]
        public int LineNumber { get; set; }
    }
}