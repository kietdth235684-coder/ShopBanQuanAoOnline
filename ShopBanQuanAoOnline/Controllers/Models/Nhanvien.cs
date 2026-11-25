using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShopBanQuanAoOnline.Models;

/// <summary>Module 6 - Nhân viên. VaiTro quyết định quyền: ChuCuaHang / NhanVien.</summary>
[Table("NHANVIEN")]
public partial class Nhanvien
{
    [Key]
    [Column("MaNV")]
    [Display(Name = "Mã NV")]
    public int MaNv { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(100)]
    [Display(Name = "Họ tên")]
    public string Ten { get; set; } = null!;

    [Column("MaCV")]
    [Display(Name = "Chức vụ")]
    public int MaCv { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    [Display(Name = "Điện thoại")]
    public string? DienThoai { get; set; }

    // Bắt buộc nhập (kiểm tra trong NhanviensController); cột CSDL cho phép NULL nên không dùng [Required]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(100)]
    [Unicode(false)]
    public string? Email { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? MatKhau { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    [Display(Name = "Vai trò")]
    public string VaiTro { get; set; } = HangSo.VaiTro.NhanVien;

    [Display(Name = "Đang làm việc")]
    public bool TrangThai { get; set; } = true;

    [ForeignKey("MaCv")]
    [InverseProperty("Nhanviens")]
    [Display(Name = "Chức vụ")]
    public virtual Chucvu? MaCvNavigation { get; set; }
}
