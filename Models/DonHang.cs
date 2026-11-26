using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShopBanQuanAoOnline.Models;

/// <summary>
/// Module 3/4 - Đơn đặt hàng COD từ website, lưu độc lập với hóa đơn.
/// Khách chỉ tạo đơn và hủy khi đơn còn "Chờ xác nhận"; nhân viên xử lý các trạng thái còn lại.
/// </summary>
[Table("DONHANG")]
public class DonHang
{
    [Key, Column("MaDH")] public int MaDh { get; set; }
    [Column("MaKH")] public int MaKh { get; set; }
    [Column(TypeName = "datetime")] public DateTime NgayDat { get; set; }
    [StringLength(100)] public string HoTenNhan { get; set; } = null!;
    [StringLength(20), Unicode(false)] public string DienThoaiNhan { get; set; } = null!;
    [StringLength(255)] public string DiaChiNhan { get; set; } = null!;
    [StringLength(255)] public string? GhiChu { get; set; }
    public int TongTien { get; set; }
    public int TrangThai { get; set; }
    public int ThanhToan { get; set; }
    [Column("MaNV")] public int? MaNv { get; set; }
    [Column(TypeName = "datetime")] public DateTime? NgayCapNhat { get; set; }
    [StringLength(255)] public string? LyDo { get; set; }

    [ForeignKey("MaKh"), InverseProperty("DonHangs")] public virtual Khachhang? MaKhNavigation { get; set; }
    [ForeignKey("MaNv")] public virtual Nhanvien? MaNvNavigation { get; set; }
    [InverseProperty("MaDhNavigation")] public virtual ICollection<CtDonHang> CtDonHangs { get; set; } = new List<CtDonHang>();
}

[Table("CTDONHANG")]
public class CtDonHang
{
    [Key, Column("MaCTDH")] public int MaCtdh { get; set; }
    [Column("MaDH")] public int MaDh { get; set; }
    [Column("MaBT")] public int MaBt { get; set; }
    public int DonGia { get; set; }
    public int SoLuong { get; set; }
    public int ThanhTien { get; set; }

    [ForeignKey("MaDh"), InverseProperty("CtDonHangs")] public virtual DonHang? MaDhNavigation { get; set; }
    [ForeignKey("MaBt")] public virtual BienThe? MaBtNavigation { get; set; }
}

// ===================== ĐỔI TRẢ CƠ BẢN =====================

[Table("PHIEUDOITRA")]
public class PhieuDoiTra
{
    [Key, Column("MaPDT")] public int MaPdt { get; set; }
    [Column("MaHD")] public int MaHd { get; set; }
    [Column(TypeName = "datetime")] public DateTime NgayLap { get; set; }
    [Column("MaNV")] public int? MaNv { get; set; }
    [StringLength(255)] public string LyDo { get; set; } = null!;
    public int TienHoan { get; set; }

    [ForeignKey("MaHd")] public virtual Hoadon? MaHdNavigation { get; set; }
    [ForeignKey("MaNv")] public virtual Nhanvien? MaNvNavigation { get; set; }
    [InverseProperty("MaPdtNavigation")] public virtual ICollection<CtDoiTra> CtDoiTras { get; set; } = new List<CtDoiTra>();
}

[Table("CTDOITRA")]
public class CtDoiTra
{
    public const string Tra = "TRA";
    public const string Doi = "DOI";

    [Key, Column("MaCTDT")] public int MaCtdt { get; set; }
    [Column("MaPDT")] public int MaPdt { get; set; }
    [Column("MaCTHD")] public int MaCthd { get; set; }
    [Column("MaBTTra")] public int MaBtTra { get; set; }
    public int SoLuong { get; set; }
    [StringLength(10), Unicode(false)] public string HinhThuc { get; set; } = Tra;
    [Column("MaBTDoi")] public int? MaBtDoi { get; set; }
    public bool NhapLaiKho { get; set; } = true;
    public int DonGia { get; set; }
    public int TienHoan { get; set; }

    [ForeignKey("MaPdt"), InverseProperty("CtDoiTras")] public virtual PhieuDoiTra? MaPdtNavigation { get; set; }
    [ForeignKey("MaCthd")] public virtual Cthoadon? MaCthdNavigation { get; set; }
    [ForeignKey("MaBtTra")] public virtual BienThe? MaBtTraNavigation { get; set; }
    [ForeignKey("MaBtDoi")] public virtual BienThe? MaBtDoiNavigation { get; set; }
}

// ===================== MODULE 5: ĐÁNH GIÁ =====================

[Table("DANHGIA")]
public class DanhGia
{
    [Key, Column("MaDG")] public int MaDg { get; set; }
    [Column("MaKH")] public int MaKh { get; set; }
    [Column("MaMH")] public int MaMh { get; set; }
    public int SoSao { get; set; }
    [StringLength(1000)] public string? NhanXet { get; set; }
    [Column(TypeName = "datetime")] public DateTime NgayDanhGia { get; set; }
    public int TrangThai { get; set; }

    [ForeignKey("MaKh")] public virtual Khachhang? MaKhNavigation { get; set; }
    [ForeignKey("MaMh"), InverseProperty("DanhGias")] public virtual Mathang? MaMhNavigation { get; set; }
}

// ===================== MODULE 6: CHẤM CÔNG CƠ BẢN =====================

[Table("CHAMCONG")]
public class ChamCong
{
    [Key, Column("MaCC")] public int MaCc { get; set; }
    [Column("MaNV")] public int MaNv { get; set; }
    [Column(TypeName = "date")] public DateTime Ngay { get; set; }
    [Column(TypeName = "datetime")] public DateTime GioVao { get; set; }
    [Column(TypeName = "datetime")] public DateTime? GioRa { get; set; }
    [StringLength(255)] public string? GhiChu { get; set; }

    [ForeignKey("MaNv")] public virtual Nhanvien? MaNvNavigation { get; set; }

    [NotMapped] public double SoGio => GioRa.HasValue ? Math.Round((GioRa.Value - GioVao).TotalHours, 2) : 0;
}
