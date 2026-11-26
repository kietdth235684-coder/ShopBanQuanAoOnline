using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShopBanQuanAoOnline.Models;

// ===================== MODULE 2: KHO VÀ NHÀ CUNG CẤP =====================

[Table("NHACUNGCAP")]
public class NhaCungCap
{
    [Key, Column("MaNCC"), Display(Name = "Mã NCC")] public int MaNcc { get; set; }

    [Required(ErrorMessage = "Nhập tên nhà cung cấp"), StringLength(150), Display(Name = "Tên nhà cung cấp")]
    public string Ten { get; set; } = null!;

    [StringLength(20), Unicode(false), Display(Name = "Điện thoại")] public string? DienThoai { get; set; }
    [StringLength(100), Unicode(false), EmailAddress(ErrorMessage = "Email không hợp lệ")] public string? Email { get; set; }
    [StringLength(200), Display(Name = "Địa chỉ")] public string? DiaChi { get; set; }
    [StringLength(255), Display(Name = "Ghi chú")] public string? GhiChu { get; set; }
    [Display(Name = "Đang hợp tác")] public bool TrangThai { get; set; } = true;

    [InverseProperty("MaNccNavigation")] public virtual ICollection<PhieuNhap> PhieuNhaps { get; set; } = new List<PhieuNhap>();
}

[Table("PHIEUNHAP")]
public class PhieuNhap
{
    [Key, Column("MaPN")] public int MaPn { get; set; }
    [Column(TypeName = "datetime")] public DateTime NgayNhap { get; set; }
    [Column("MaNCC")] public int MaNcc { get; set; }
    [Column("MaNV")] public int? MaNv { get; set; }
    public int TongTien { get; set; }
    [StringLength(255)] public string? GhiChu { get; set; }

    [ForeignKey("MaNcc"), InverseProperty("PhieuNhaps")] public virtual NhaCungCap? MaNccNavigation { get; set; }
    [ForeignKey("MaNv")] public virtual Nhanvien? MaNvNavigation { get; set; }
    [InverseProperty("MaPnNavigation")] public virtual ICollection<CtPhieuNhap> CtPhieuNhaps { get; set; } = new List<CtPhieuNhap>();
}

[Table("CTPHIEUNHAP")]
public class CtPhieuNhap
{
    [Key, Column("MaCTPN")] public int MaCtpn { get; set; }
    [Column("MaPN")] public int MaPn { get; set; }
    [Column("MaBT")] public int MaBt { get; set; }
    public int SoLuong { get; set; }
    public int DonGiaNhap { get; set; }
    public int ThanhTien { get; set; }

    [ForeignKey("MaPn"), InverseProperty("CtPhieuNhaps")] public virtual PhieuNhap? MaPnNavigation { get; set; }
    [ForeignKey("MaBt")] public virtual BienThe? MaBtNavigation { get; set; }
}

[Table("PHIEUXUAT")]
public class PhieuXuat
{
    [Key, Column("MaPX")] public int MaPx { get; set; }
    [Column(TypeName = "datetime")] public DateTime NgayXuat { get; set; }
    [Column("MaNV")] public int? MaNv { get; set; }
    [StringLength(100)] public string LyDo { get; set; } = null!;
    [StringLength(255)] public string? GhiChu { get; set; }

    [ForeignKey("MaNv")] public virtual Nhanvien? MaNvNavigation { get; set; }
    [InverseProperty("MaPxNavigation")] public virtual ICollection<CtPhieuXuat> CtPhieuXuats { get; set; } = new List<CtPhieuXuat>();
}

[Table("CTPHIEUXUAT")]
public class CtPhieuXuat
{
    [Key, Column("MaCTPX")] public int MaCtpx { get; set; }
    [Column("MaPX")] public int MaPx { get; set; }
    [Column("MaBT")] public int MaBt { get; set; }
    public int SoLuong { get; set; }

    [ForeignKey("MaPx"), InverseProperty("CtPhieuXuats")] public virtual PhieuXuat? MaPxNavigation { get; set; }
    [ForeignKey("MaBt")] public virtual BienThe? MaBtNavigation { get; set; }
}

[Table("PHIEUKIEMKE")]
public class PhieuKiemKe
{
    [Key, Column("MaKK")] public int MaKk { get; set; }
    [Column(TypeName = "datetime")] public DateTime NgayKiemKe { get; set; }
    [Column("MaNV")] public int? MaNv { get; set; }
    [StringLength(255)] public string? GhiChu { get; set; }

    [ForeignKey("MaNv")] public virtual Nhanvien? MaNvNavigation { get; set; }
    [InverseProperty("MaKkNavigation")] public virtual ICollection<CtKiemKe> CtKiemKes { get; set; } = new List<CtKiemKe>();
}

[Table("CTKIEMKE")]
public class CtKiemKe
{
    [Key, Column("MaCTKK")] public int MaCtkk { get; set; }
    [Column("MaKK")] public int MaKk { get; set; }
    [Column("MaBT")] public int MaBt { get; set; }
    public int SoLuongHeThong { get; set; }
    public int SoLuongThucTe { get; set; }
    public int ChenhLech { get; set; }

    [ForeignKey("MaKk"), InverseProperty("CtKiemKes")] public virtual PhieuKiemKe? MaKkNavigation { get; set; }
    [ForeignKey("MaBt")] public virtual BienThe? MaBtNavigation { get; set; }
}

/// <summary>Thẻ kho: mỗi biến động tồn kho = 1 dòng. Là nguồn dữ liệu cho báo cáo nhập - xuất - tồn.</summary>
[Table("THEKHO")]
public class TheKho
{
    [Key, Column("MaTK")] public int MaTk { get; set; }
    [Column("MaBT")] public int MaBt { get; set; }
    [Column(TypeName = "datetime")] public DateTime Ngay { get; set; }
    [StringLength(20), Unicode(false)] public string Loai { get; set; } = null!;
    public int SoLuong { get; set; }
    public int TonSau { get; set; }
    [StringLength(50)] public string? ChungTu { get; set; }
    [Column("MaNV")] public int? MaNv { get; set; }

    [ForeignKey("MaBt")] public virtual BienThe? MaBtNavigation { get; set; }
    [ForeignKey("MaNv")] public virtual Nhanvien? MaNvNavigation { get; set; }
}
