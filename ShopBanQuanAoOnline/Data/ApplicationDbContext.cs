using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Chucvu> Chucvus { get; set; }

    public virtual DbSet<Cthoadon> Cthoadons { get; set; }

    public virtual DbSet<Cuahang> Cuahangs { get; set; }

    public virtual DbSet<Danhmuc> Danhmucs { get; set; }

    public virtual DbSet<Diachi> Diachis { get; set; }

    public virtual DbSet<Hoadon> Hoadons { get; set; }

    public virtual DbSet<Khachhang> Khachhangs { get; set; }

    public virtual DbSet<Mathang> Mathangs { get; set; }

    public virtual DbSet<Nhanvien> Nhanviens { get; set; }

    // ===== Bảng bổ sung theo báo cáo phạm vi =====
    public virtual DbSet<BienThe> BienThes { get; set; }          // Module 1: size / màu
    public virtual DbSet<NhaCungCap> NhaCungCaps { get; set; }    // Module 2
    public virtual DbSet<PhieuNhap> PhieuNhaps { get; set; }
    public virtual DbSet<CtPhieuNhap> CtPhieuNhaps { get; set; }
    public virtual DbSet<PhieuXuat> PhieuXuats { get; set; }
    public virtual DbSet<CtPhieuXuat> CtPhieuXuats { get; set; }
    public virtual DbSet<PhieuKiemKe> PhieuKiemKes { get; set; }
    public virtual DbSet<CtKiemKe> CtKiemKes { get; set; }
    public virtual DbSet<TheKho> TheKhos { get; set; }
    public virtual DbSet<DonHang> DonHangs { get; set; }          // Module 3/4: đơn COD
    public virtual DbSet<CtDonHang> CtDonHangs { get; set; }
    public virtual DbSet<PhieuDoiTra> PhieuDoiTras { get; set; }  // Module 3: đổi trả
    public virtual DbSet<CtDoiTra> CtDoiTras { get; set; }
    public virtual DbSet<DanhGia> DanhGias { get; set; }          // Module 5
    public virtual DbSet<ChamCong> ChamCongs { get; set; }        // Module 6

    // Chuỗi kết nối KHÔNG đặt trong code: được cấu hình ở appsettings.json (ConnectionStrings:DefaultConnection)
    // và nạp qua AddDbContext trong Program.cs.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chucvu>(entity =>
        {
            entity.HasKey(e => e.MaCv).HasName("PK__CHUCVU__27258E76F5ED3AC9");

            entity.Property(e => e.HeSo).HasDefaultValue(1.0);
        });

        modelBuilder.Entity<Cthoadon>(entity =>
        {
            entity.HasKey(e => e.MaCthd).HasName("PK__CTHOADON__1E4FA77130A35967");

            entity.Property(e => e.DonGia).HasDefaultValue(0);
            entity.Property(e => e.SoLuong).HasDefaultValue((short)1);

            entity.HasOne(d => d.MaHdNavigation).WithMany(p => p.Cthoadons)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CTHOADON__MaHD__5EBF139D");

            entity.HasOne(d => d.MaMhNavigation).WithMany(p => p.Cthoadons)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CTHOADON__MaMH__5FB337D6");
        });

        modelBuilder.Entity<Cuahang>(entity =>
        {
            entity.HasKey(e => e.MaCh).HasName("PK__CUAHANG__27258E00D1007137");
        });

        modelBuilder.Entity<Danhmuc>(entity =>
        {
            entity.HasKey(e => e.MaDm).HasName("PK__DANHMUC__2725866E5BB150FD");
        });

        modelBuilder.Entity<Diachi>(entity =>
        {
            entity.HasKey(e => e.MaDc).HasName("PK__DIACHI__272586647051C520");

            entity.Property(e => e.MacDinh).HasDefaultValue(1);

            entity.HasOne(d => d.MaKhNavigation).WithMany(p => p.Diachis)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DIACHI__MaKH__52593CB8");
        });

        modelBuilder.Entity<Hoadon>(entity =>
        {
            entity.HasKey(e => e.MaHd).HasName("PK__HOADON__2725A6E02E23DD83");

            entity.Property(e => e.Ngay).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TongTien).HasDefaultValue(0);
            entity.Property(e => e.TrangThai).HasDefaultValue(0);

            entity.HasOne(d => d.MaKhNavigation).WithMany(p => p.Hoadons)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Khachhang>(entity =>
        {
            entity.HasKey(e => e.MaKh).HasName("PK__KHACHHAN__2725CF1EEAA2E5BA");
        });

        modelBuilder.Entity<Mathang>(entity =>
        {
            entity.HasKey(e => e.MaMh).HasName("PK__MATHANG__2725DFD946DA7860");

            entity.Property(e => e.GiaBan).HasDefaultValue(0);
            entity.Property(e => e.GiaGoc).HasDefaultValue(0);
            entity.Property(e => e.LuotMua).HasDefaultValue(0);
            entity.Property(e => e.LuotXem).HasDefaultValue(0);
            entity.Property(e => e.SoLuong).HasDefaultValue((short)0);

            entity.HasOne(d => d.MaDmNavigation).WithMany(p => p.Mathangs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MATHANG__MaDM__45F365D3");
        });

        modelBuilder.Entity<Nhanvien>(entity =>
        {
            entity.HasKey(e => e.MaNv).HasName("PK__NHANVIEN__2725D70A3CB9074D");

            entity.HasOne(d => d.MaCvNavigation).WithMany(p => p.Nhanviens)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__NHANVIEN__MaCV__4D94879B");
        });

        // Các bảng mới: giá trị luôn được gán trong code nên không khai báo default ở EF.
        modelBuilder.Entity<CtDoiTra>(entity =>
        {
            entity.HasOne(d => d.MaBtTraNavigation).WithMany().HasForeignKey(d => d.MaBtTra).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.MaBtDoiNavigation).WithMany().HasForeignKey(d => d.MaBtDoi).OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
