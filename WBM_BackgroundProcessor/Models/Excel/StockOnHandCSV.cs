using CsvHelper.Configuration.Attributes;

namespace WBM_BackgroundProcessor.Models.Excel
{
    // One line of the stock on hand CSV (RP_<Site>_CSR2_..._<ddMMyyHHmmss>.csv).
    // Only the columns we save are mapped, the rest of the file is ignored.
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

        // Line in the file this row came from, filled in when reading, so log messages can point to it
        [Ignore]
        public int LineNumber { get; set; }
    }
}