using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShopBanQuanAoOnline.Models;

[Table("DIACHI")]
public partial class Diachi
{
    [Key]
    [Column("MaDC")]
    public int MaDc { get; set; }

    [Column("MaKH")]
    public int MaKh { get; set; }

    [Column("DiaChi")]
    [StringLength(100)]
    [Display(Name = "Địa chỉ")]
    public string DiaChi1 { get; set; } = null!;

    [StringLength(50)]
    [Display(Name = "Phường/Xã")]
    public string? PhuongXa { get; set; }

    [StringLength(50)]
    [Display(Name = "Quận/Huyện")]
    public string? QuanHuyen { get; set; }

    [StringLength(50)]
    [Display(Name = "Tỉnh/Thành")]
    public string? TinhThanh { get; set; }

    public int? MacDinh { get; set; }

    [ForeignKey("MaKh")]
    [InverseProperty("Diachis")]
    public virtual Khachhang MaKhNavigation { get; set; } = null!;

    [NotMapped]
    public string DayDu => string.Join(", ", new[] { DiaChi1, PhuongXa, QuanHuyen, TinhThanh }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
