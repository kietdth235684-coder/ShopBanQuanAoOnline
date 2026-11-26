using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBanQuanAoOnline.Models;

/// <summary>Module 1/2 - Biến thể sản phẩm theo kích cỡ + màu sắc, nơi lưu tồn kho thực tế.</summary>
[Table("BIENTHE")]
public class BienThe
{
    [Key, Column("MaBT")] public int MaBt { get; set; }
    [Column("MaMH")] public int MaMh { get; set; }

    [Required(ErrorMessage = "Nhập kích cỡ"), StringLength(20), Display(Name = "Kích cỡ")]
    public string KichCo { get; set; } = null!;

    [Required(ErrorMessage = "Nhập màu sắc"), StringLength(30), Display(Name = "Màu sắc")]
    public string MauSac { get; set; } = null!;

    [Display(Name = "Tồn kho")] public int SoLuongTon { get; set; }
    [Display(Name = "Đang bán")] public bool TrangThai { get; set; } = true;

    [ForeignKey("MaMh"), InverseProperty("BienThes")]
    public virtual Mathang? MaMhNavigation { get; set; }

    [NotMapped] public string TenBienThe => $"{KichCo} / {MauSac}";
}
