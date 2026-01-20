using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public class paperlessdbcontext : DbContext
    {
        public paperlessdbcontext() { }

        public paperlessdbcontext(DbContextOptions<paperlessdbcontext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
            base.OnConfiguring(optionsBuilder);
        }

        public static string ConnectionString { get; set; }

        public DbSet<CARRIERS_DW> CARRIERS_DW { get; set; }

        public DbSet<CUSTOMER_DW> CUSTOMER_DW { get; set; }

        public DbSet<DB_TRANS> DB_TRANS { get; set; }

        public DbSet<PICK_DETAIL_DW> PICK_DETAIL_DW { get; set; }

        public DbSet<PICK_HEAD_DW> PICK_HEAD_DW { get; set; }

        public DbSet<POSITION_DW> POSTION_DW { get; set; }

        public DbSet<PURCH_ORD_PROD_DW> PURCH_ORD_PROD_DW { get; set; }

        public DbSet<STOCK_DW> STOCK_DW { get; set; }

        public DbSet<STOCK_MOVE_DW> STOCK_MOVE_DW { get; set; }

        public DbSet<TRIGGER_CARRIERS_DW> TRIGGER_CARRIERS_DW { get; set; }

        public DbSet<TRIGGER_CUSTOMER_DW> TRIGGER_CUSTOMER_DW { get; set; }

        public DbSet<TRIGGER_PICK_HEAD_DW> TRIGGER_PICK_HEAD_DW { get; set; }

        public DbSet<TRIGGER_POSITION_DW> TRIGGER_POSITION_DW { get; set; }

        public DbSet<TRIGGER_PURCH_ORD_PROD_DW> TRIGGER_PURCH_ORD_PROD_DW { get; set; }

        public DbSet<TRIGGER_STOCK_DW> TRIGGER_STOCK_DW { get; set; }

        public DbSet<TRUCK_LOAD_DW> TRUCK_LOAD_DW { get; set; }

    }
}
