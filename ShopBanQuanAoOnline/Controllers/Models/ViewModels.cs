namespace ShopBanQuanAoOnline.Models;

// ===== Module 7: báo cáo =====
public class DongDoanhThu
{
    public string Ky { get; set; } = "";
    public int SoHoaDon { get; set; }
    public long DoanhThuTaiQuay { get; set; }
    public long DoanhThuCod { get; set; }
    public long DoanhThu => DoanhThuTaiQuay + DoanhThuCod;
    public long GiaVon { get; set; }
    public long HoanTra { get; set; }
    public long DieuChinhGiaVonDoiTra { get; set; }
    public long DoanhThuThuan => DoanhThu - HoanTra;
    public long LoiNhuanUocTinh => DoanhThu - GiaVon - HoanTra + DieuChinhGiaVonDoiTra;
}

public class BaoCaoDoanhThuVM
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public string Kieu { get; set; } = "ngay";
    public List<DongDoanhThu> Dong { get; set; } = new();
    public DongDoanhThu Tong { get; set; } = new() { Ky = "Tổng" };
    public int SoDonCodChuaHoanTat { get; set; }
    public long GiaTriCodChuaHoanTat { get; set; }
}

public class DongBanChay
{
    public int MaMh { get; set; }
    public string Ten { get; set; } = "";
    public string? HinhAnh { get; set; }
    public int SoLuongBan { get; set; }
    public long DoanhThu { get; set; }
}

public class DongNhapXuatTon
{
    public int MaBt { get; set; }
    public string TenSanPham { get; set; } = "";
    public string BienThe { get; set; } = "";
    public int TonDau { get; set; }
    public int Nhap { get; set; }
    public int XuatBan { get; set; }
    public int XuatKhac { get; set; }
    public int DoiTra { get; set; }
    public int KiemKe { get; set; }
    public int TonCuoi { get; set; }
}

public class DashboardVM
{
    public bool LaChuCuaHang { get; set; }
    public long DoanhThuHomNay { get; set; }
    public int HoaDonHomNay { get; set; }
    public long DoanhThuThangNay { get; set; }
    public int DonChoXacNhan { get; set; }
    public int DonDangXuLy { get; set; }
    public int DanhGiaChoDuyet { get; set; }
    public int NguongTonThap { get; set; }
    public List<BienThe> TonThap { get; set; } = new();
    public ChamCong? ChamCongHomNay { get; set; }
}

// ===== Module 4: bộ lọc danh sách sản phẩm trên website =====
public class LocSanPham
{
    public int? MaDm { get; set; }
    public string? TuKhoa { get; set; }
    public string? Size { get; set; }
    public string? Mau { get; set; }
    public int? GiaTu { get; set; }
    public int? GiaDen { get; set; }
    public string? SapXep { get; set; }
}
