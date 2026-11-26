using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShopBanQuanAoOnline.Models;

[Table("CHUCVU")]
public partial class Chucvu
{
    [Key]
    [Column("MaCV")]
    public int MaCv { get; set; }

    [StringLength(100)]
    [Display(Name = "Tên")]
    public string Ten { get; set; } = null!;

    public double? HeSo { get; set; }

    [InverseProperty("MaCvNavigation")]
    public virtual ICollection<Nhanvien> Nhanviens { get; set; } = new List<Nhanvien>();
}
