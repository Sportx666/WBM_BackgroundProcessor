using NLog;
using NLog.Web;

namespace WBM_BackgroundProcessor.WBM_DB
{
    public partial class WBMDatabase
    {
        private readonly WBMdbcontext _dbContext;
        public WBMdbcontext dbContext
        {
            get { return _dbContext; }
        }

        public WBMDatabase(WBMdbcontext dbContext)
        {
            _dbContext = dbContext;
        }

        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

    }
}
