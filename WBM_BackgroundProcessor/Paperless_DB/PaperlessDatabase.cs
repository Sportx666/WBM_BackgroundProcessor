namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        private readonly paperlessdbcontext _dbContext;
        public paperlessdbcontext dbContext
        {
            get { return _dbContext; }
        }

        public PaperlessDatabase(paperlessdbcontext dbContext)
        {
            _dbContext = dbContext;
        }

        //private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

    }
}
