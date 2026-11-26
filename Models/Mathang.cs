using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShopBanQuanAoOnline.Models;

/// <summary>Module 1 - Sản phẩm. Tồn kho chi tiết nằm ở BIENTHE (size/màu); SoLuong = tổng tồn các biến thể.</summary>
[Table("MATHANG")]
public partial class Mathang
{
    [Key]
    [Column("MaMH")]
    [Display(Name = "Mã SP")]
    public int MaMh { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
    [StringLength(100)]
    [Display(Name = "Tên sản phẩm")]
    public string Ten { get; set; } = null!;

    [Range(0, int.MaxValue, ErrorMessage = "Giá không hợp lệ")]
    [Display(Name = "Giá niêm yết")]
    public int? GiaGoc { get; set; }

    // Bắt buộc nhập (kiểm tra trong MathangsController); cột CSDL cho phép NULL nên không dùng [Required] để mô hình EF khớp CSDL
    [Range(0, int.MaxValue, ErrorMessage = "Giá không hợp lệ")]
    [Display(Name = "Giá bán")]
    public int? GiaBan { get; set; }

    /// <summary>Giá nhập tham khảo (bình quân theo phiếu nhập) - dùng tính lợi nhuận ước tính.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Giá không hợp lệ")]
    [Display(Name = "Giá nhập tham khảo")]
    public int GiaNhap { get; set; }

    [Display(Name = "Tồn kho")]
    public short? SoLuong { get; set; }

    [StringLength(1000)]
    [Display(Name = "Mô tả")]
    public string? MoTa { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    [Display(Name = "Hình ảnh")]
    public string? HinhAnh { get; set; }

    [Column("MaDM")]
    [Display(Name = "Danh mục")]
    public int MaDm { get; set; }

    [Display(Name = "Lượt xem")]
    public int? LuotXem { get; set; }

    [Display(Name = "Lượt mua")]
    public int? LuotMua { get; set; }

    /// <summary>true: đang bán (hiển thị trên website), false: ngừng bán.</summary>
    [Display(Name = "Đang bán")]
    public bool TrangThai { get; set; } = true;

    [InverseProperty("MaMhNavigation")]
    public virtual ICollection<Cthoadon> Cthoadons { get; set; } = new List<Cthoadon>();

    [InverseProperty("MaMhNavigation")]
    public virtual ICollection<BienThe> BienThes { get; set; } = new List<BienThe>();

    [InverseProperty("MaMhNavigation")]
    public virtual ICollection<DanhGia> DanhGias { get; set; } = new List<DanhGia>();

    [ForeignKey("MaDm")]
    [InverseProperty("Mathangs")]
    [Display(Name = "Danh mục")]
    public virtual Danhmuc? MaDmNavigation { get; set; }
}
