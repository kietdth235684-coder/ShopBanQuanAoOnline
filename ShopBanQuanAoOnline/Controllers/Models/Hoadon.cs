using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShopBanQuanAoOnline.Models;

/// <summary>
/// Module 3 - Hóa đơn bán hàng nội bộ. Chỉ được tạo khi giao dịch hoàn tất:
/// bán tại quầy đã thanh toán (LoaiHD = 0) hoặc đơn COD giao thành công và đã thu tiền (LoaiHD = 1, MaDH duy nhất).
/// TrangThai = 1: hoàn tất (tính doanh thu); 0: dữ liệu cũ trước khi nâng cấp (không tính doanh thu).
/// </summary>
[Table("HOADON")]
public partial class Hoadon
{
    [Key]
    [Column("MaHD")]
    [Display(Name = "Mã HĐ")]
    public int MaHd { get; set; }

    [Column(TypeName = "datetime")]
    [Display(Name = "Ngày lập")]
    public DateTime? Ngay { get; set; }

    [Display(Name = "Tổng tiền")]
    public int? TongTien { get; set; }

    [Column("MaKH")]
    public int? MaKh { get; set; }

    public int? TrangThai { get; set; }

    [Column("LoaiHD")]
    public int LoaiHd { get; set; }

    [Column("MaDH")]
    public int? MaDh { get; set; }

    [Column("MaNV")]
    public int? MaNv { get; set; }

    public int? TienKhachDua { get; set; }

    [StringLength(255)]
    public string? GhiChu { get; set; }

    [InverseProperty("MaHdNavigation")]
    public virtual ICollection<Cthoadon> Cthoadons { get; set; } = new List<Cthoadon>();

    [ForeignKey("MaKh")]
    [InverseProperty("Hoadons")]
    public virtual Khachhang? MaKhNavigation { get; set; }

    [ForeignKey("MaDh")]
    public virtual DonHang? MaDhNavigation { get; set; }

    [ForeignKey("MaNv")]
    public virtual Nhanvien? MaNvNavigation { get; set; }
}
