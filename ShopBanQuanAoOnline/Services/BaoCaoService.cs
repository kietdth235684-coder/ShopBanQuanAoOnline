using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Services;

/// <summary>
/// Module 7 - Báo cáo. Doanh thu CHỈ lấy từ hóa đơn hoàn tất (TrangThai = 1):
/// hóa đơn tại quầy + hóa đơn sinh ra khi đơn COD giao thành công và đã thu tiền.
/// Đơn COD chưa hoàn tất / hủy / giao thất bại không có hóa đơn nên không bao giờ được cộng.
/// </summary>
public class BaoCaoService
{
    private readonly ApplicationDbContext _db;
    public BaoCaoService(ApplicationDbContext db) => _db = db;

    private static string NhanKy(DateTime d, string kieu) => kieu switch
    {
        "nam" => d.Year.ToString(),
        "thang" => d.ToString("MM/yyyy", CultureInfo.InvariantCulture),
        _ => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
    };

    public async Task<BaoCaoDoanhThuVM> DoanhThuAsync(DateTime tu, DateTime den, string kieu)
    {
        kieu = kieu is "thang" or "nam" ? kieu : "ngay";
        var tuNgay = tu.Date;
        var denNgay = den.Date.AddDays(1); // lấy hết ngày cuối

        var hoaDons = await _db.Hoadons.AsNoTracking()
            .Where(h => h.TrangThai == HangSo.TrangThaiHoaDon.HoanTat && h.Ngay >= tuNgay && h.Ngay < denNgay)
            .Select(h => new
            {
                Ngay = h.Ngay!.Value,
                h.LoaiHd,
                Tong = (long)(h.TongTien ?? 0),
                GiaVon = h.Cthoadons.Sum(c => (long)c.GiaVon * (c.SoLuong ?? 0))
            }).ToListAsync();

        var doiTras = await _db.CtDoiTras.AsNoTracking()
            .Where(c => c.MaPdtNavigation!.NgayLap >= tuNgay && c.MaPdtNavigation.NgayLap < denNgay)
            .Select(c => new
            {
                Ngay = c.MaPdtNavigation!.NgayLap,
                c.TienHoan,
                c.SoLuong,
                c.NhapLaiKho,
                c.HinhThuc,
                GiaVon = c.MaCthdNavigation!.GiaVon
            }).ToListAsync();

        var kq = new Dictionary<string, DongDoanhThu>();
        DongDoanhThu Lay(DateTime d)
        {
            var k = NhanKy(d, kieu);
            if (!kq.TryGetValue(k, out var dong)) kq[k] = dong = new DongDoanhThu { Ky = k };
            return dong;
        }
        foreach (var h in hoaDons)
        {
            var d = Lay(h.Ngay);
            d.SoHoaDon++;
            if (h.LoaiHd == HangSo.LoaiHoaDon.Cod) d.DoanhThuCod += h.Tong; else d.DoanhThuTaiQuay += h.Tong;
            d.GiaVon += h.GiaVon;
        }
        foreach (var c in doiTras)
        {
            var d = Lay(c.Ngay);
            d.HoanTra += c.TienHoan;
            long gv = (long)c.GiaVon * c.SoLuong;
            if (c.NhapLaiKho) d.DieuChinhGiaVonDoiTra += gv;            // hàng nhận lại còn bán được
            if (c.HinhThuc == CtDoiTra.Doi) d.DieuChinhGiaVonDoiTra -= gv; // hàng mới xuất ra để đổi
        }

        var vm = new BaoCaoDoanhThuVM { TuNgay = tuNgay, DenNgay = den.Date, Kieu = kieu };
        vm.Dong = kq.Values.OrderBy(x => SapXep(x.Ky, kieu)).ToList();
        vm.Tong = new DongDoanhThu
        {
            Ky = "Tổng cộng",
            SoHoaDon = vm.Dong.Sum(x => x.SoHoaDon),
            DoanhThuTaiQuay = vm.Dong.Sum(x => x.DoanhThuTaiQuay),
            DoanhThuCod = vm.Dong.Sum(x => x.DoanhThuCod),
            GiaVon = vm.Dong.Sum(x => x.GiaVon),
            HoanTra = vm.Dong.Sum(x => x.HoanTra),
            DieuChinhGiaVonDoiTra = vm.Dong.Sum(x => x.DieuChinhGiaVonDoiTra)
        };

        // Thông tin tham khảo (KHÔNG cộng vào doanh thu): đơn COD đặt trong kỳ nhưng chưa hoàn tất
        var choXuLy = new[] { HangSo.TrangThaiDon.ChoXacNhan, HangSo.TrangThaiDon.DaXacNhan, HangSo.TrangThaiDon.DangGiao };
        var cod = _db.DonHangs.Where(d => choXuLy.Contains(d.TrangThai) && d.NgayDat >= tuNgay && d.NgayDat < denNgay);
        vm.SoDonCodChuaHoanTat = await cod.CountAsync();
        vm.GiaTriCodChuaHoanTat = await cod.SumAsync(d => (long?)d.TongTien) ?? 0;
        return vm;
    }

    private static DateTime SapXep(string ky, string kieu) => kieu switch
    {
        "nam" => new DateTime(int.Parse(ky), 1, 1),
        "thang" => DateTime.ParseExact("01/" + ky, "dd/MM/yyyy", CultureInfo.InvariantCulture),
        _ => DateTime.ParseExact(ky, "dd/MM/yyyy", CultureInfo.InvariantCulture)
    };

    public async Task<List<DongBanChay>> BanChayAsync(DateTime tu, DateTime den, int top)
    {
        var tuNgay = tu.Date;
        var denNgay = den.Date.AddDays(1);
        return await _db.Cthoadons.AsNoTracking()
            .Where(c => c.MaHdNavigation.TrangThai == HangSo.TrangThaiHoaDon.HoanTat
                     && c.MaHdNavigation.Ngay >= tuNgay && c.MaHdNavigation.Ngay < denNgay)
            .GroupBy(c => new { c.MaMh, c.MaMhNavigation.Ten, c.MaMhNavigation.HinhAnh })
            .Select(g => new DongBanChay
            {
                MaMh = g.Key.MaMh,
                Ten = g.Key.Ten,
                HinhAnh = g.Key.HinhAnh,
                SoLuongBan = g.Sum(x => (int)(x.SoLuong ?? 0)),
                DoanhThu = g.Sum(x => (long)(x.ThanhTien ?? 0))
            })
            .OrderByDescending(x => x.SoLuongBan).ThenByDescending(x => x.DoanhThu)
            .Take(top)
            .ToListAsync();
    }

    /// <summary>Nhập - xuất - tồn theo biến thể, tính hoàn toàn từ thẻ kho.</summary>
    public async Task<List<DongNhapXuatTon>> NhapXuatTonAsync(DateTime tu, DateTime den, int? maDm)
    {
        var tuNgay = tu.Date;
        var denNgay = den.Date.AddDays(1);
        var bienThes = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation)
            .Where(b => maDm == null || b.MaMhNavigation!.MaDm == maDm)
            .OrderBy(b => b.MaMhNavigation!.Ten).ThenBy(b => b.KichCo).ThenBy(b => b.MauSac)
            .ToListAsync();
        var ids = bienThes.Select(b => b.MaBt).ToList();

        var tonDau = await _db.TheKhos.AsNoTracking().Where(t => ids.Contains(t.MaBt) && t.Ngay < tuNgay)
            .GroupBy(t => t.MaBt).Select(g => new { g.Key, Sl = g.Sum(x => x.SoLuong) })
            .ToDictionaryAsync(x => x.Key, x => x.Sl);
        var trongKy = await _db.TheKhos.AsNoTracking().Where(t => ids.Contains(t.MaBt) && t.Ngay >= tuNgay && t.Ngay < denNgay)
            .GroupBy(t => new { t.MaBt, t.Loai }).Select(g => new { g.Key.MaBt, g.Key.Loai, Sl = g.Sum(x => x.SoLuong) })
            .ToListAsync();

        var kq = new List<DongNhapXuatTon>();
        foreach (var b in bienThes)
        {
            var dong = new DongNhapXuatTon
            {
                MaBt = b.MaBt,
                TenSanPham = b.MaMhNavigation?.Ten ?? "",
                BienThe = b.TenBienThe,
                TonDau = tonDau.GetValueOrDefault(b.MaBt)
            };
            foreach (var x in trongKy.Where(t => t.MaBt == b.MaBt))
            {
                switch (x.Loai)
                {
                    case HangSo.LoaiTheKho.Nhap:
                    case HangSo.LoaiTheKho.KhoiTao: dong.Nhap += x.Sl; break;
                    case HangSo.LoaiTheKho.BanQuay:
                    case HangSo.LoaiTheKho.BanCod:
                    case HangSo.LoaiTheKho.HoanCod: dong.XuatBan -= x.Sl; break;
                    case HangSo.LoaiTheKho.Xuat: dong.XuatKhac -= x.Sl; break;
                    case HangSo.LoaiTheKho.DoiTraNhap:
                    case HangSo.LoaiTheKho.DoiTraXuat: dong.DoiTra += x.Sl; break;
                    case HangSo.LoaiTheKho.KiemKe: dong.KiemKe += x.Sl; break;
                }
            }
            dong.TonCuoi = dong.TonDau + dong.Nhap - dong.XuatBan - dong.XuatKhac + dong.DoiTra + dong.KiemKe;
            kq.Add(dong);
        }
        return kq;
    }
}
