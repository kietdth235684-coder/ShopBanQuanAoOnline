using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;
using ShopBanQuanAoOnline.Services;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Trang tổng quan khu quản trị.</summary>
    public class HomeController : AdminController
    {
        private readonly ApplicationDbContext _db;
        private readonly KhoService _kho;

        public HomeController(ApplicationDbContext db, KhoService kho)
        {
            _db = db;
            _kho = kho;
        }

        public async Task<IActionResult> Index()
        {
            var homNay = DateTime.Today;
            var dauThang = new DateTime(homNay.Year, homNay.Month, 1);
            var hdHoanTat = _db.Hoadons.Where(h => h.TrangThai == HangSo.TrangThaiHoaDon.HoanTat);
            int nguong = _kho.NguongTonThap;
            int maNv = MaNvHienTai;
            var vm = new DashboardVM
            {
                LaChuCuaHang = LaChuCuaHang,
                NguongTonThap = nguong,
                DonChoXacNhan = await _db.DonHangs.CountAsync(d => d.TrangThai == HangSo.TrangThaiDon.ChoXacNhan),
                DonDangXuLy = await _db.DonHangs.CountAsync(d => d.TrangThai == HangSo.TrangThaiDon.DaXacNhan || d.TrangThai == HangSo.TrangThaiDon.DangGiao),
                DanhGiaChoDuyet = await _db.DanhGias.CountAsync(d => d.TrangThai == HangSo.TrangThaiDanhGia.ChoDuyet),
                HoaDonHomNay = await hdHoanTat.CountAsync(h => h.Ngay >= homNay),
                TonThap = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation)
                    .Where(b => b.TrangThai && b.MaMhNavigation!.TrangThai && b.SoLuongTon <= nguong)
                    .OrderBy(b => b.SoLuongTon).Take(10).ToListAsync(),
                ChamCongHomNay = await _db.ChamCongs.AsNoTracking().FirstOrDefaultAsync(c => c.MaNv == maNv && c.Ngay == homNay)
            };
            if (LaChuCuaHang)
            {
                vm.DoanhThuHomNay = await hdHoanTat.Where(h => h.Ngay >= homNay).SumAsync(h => (long?)h.TongTien) ?? 0;
                vm.DoanhThuThangNay = await hdHoanTat.Where(h => h.Ngay >= dauThang).SumAsync(h => (long?)h.TongTien) ?? 0;
            }
            return View(vm);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
