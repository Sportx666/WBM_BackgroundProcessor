using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models.WBM_DB;

namespace WBM_BackgroundProcessor.WBM_DB
{
    public class WBMdbcontext : DbContext
    {
        public WBMdbcontext() { }

        public WBMdbcontext(DbContextOptions<WBMdbcontext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
            base.OnConfiguring(optionsBuilder);
        }

        public static string ConnectionString { get; set; }

        public DbSet<ActiveServices> ActiveServices { get; set; }
    }
}
