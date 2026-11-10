using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using TempAndHumiTest.Models;

namespace TempAndHumiTest.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<UserModel> userModels { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<UserModel>(entity =>
            {
                entity.ToTable("user_manage"); // 声明表名
                entity.HasKey(x => x.Id); //声明主键
               
            });


        }
    }
}
