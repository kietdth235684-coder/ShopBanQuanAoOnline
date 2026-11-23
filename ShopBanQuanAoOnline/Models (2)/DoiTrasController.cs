using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;
using ShopBanQuanAoOnline.Services;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Module 3 - Xử lý đổi trả cơ bản, gắn với hóa đơn đã lập; điều chỉnh kho theo hàng thực nhận.</summary>
    public class DoiTrasController : AdminController
    {
        private readonly ApplicationDbContext _db;
        private readonly BanHangService _banHang;
        public DoiTrasController(ApplicationDbContext db, BanHangService banHang) { _db = db; _banHang = banHang; }

        public async Task<IActionResult> Index()
        {
            return View(await _db.PhieuDoiTras.AsNoTracking().Include(p => p.MaNvNavigation).Include(p => p.MaHdNavigation)
                .OrderByDescending(p => p.NgayLap).Take(300).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.PhieuDoiTras.AsNoTracking().Include(x => x.MaNvNavigation).Include(x => x.MaHdNavigation)
                .Include(x => x.CtDoiTras).ThenInclude(c => c.MaBtTraNavigation).ThenInclude(b => b!.MaMhNavigation)
                .Include(x => x.CtDoiTras).ThenInclude(c => c.MaBtDoiNavigation)
                .FirstOrDefaultAsync(x => x.MaPdt == id);
            return p == null ? NotFound() : View(p);
        }

        /// <summary>Bước 1: tìm hóa đơn; bước 2: chọn dòng hàng, số lượng, hình thức đổi/trả.</summary>
        public async Task<IActionResult> Create(int? maHd)
        {
            if (maHd == null) return View("ChonHoaDon");
            var hd = await _db.Hoadons.AsNoTracking().Include(h => h.MaKhNavigation)
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaMhNavigation).ThenInclude(m => m.BienThes)
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaBtNavigation)
                .FirstOrDefaultAsync(h => h.MaHd == maHd);
            if (hd == null)
            {
                Loi($"Không tìm thấy hóa đơn HD{maHd}.");
                return View("ChonHoaDon");
            }
            if (hd.TrangThai != HangSo.TrangThaiHoaDon.HoanTat)
            {
                Loi("Chỉ đổi trả cho hóa đơn đã hoàn tất.");
                return View("ChonHoaDon");
            }
            var ids = hd.Cthoadons.Select(c => c.MaCthd).ToList();
            ViewBag.DaDoiTra = await _db.CtDoiTras.Where(x => ids.Contains(x.MaCthd))
                .GroupBy(x => x.MaCthd).Select(g => new { g.Key, Sl = g.Sum(x => x.SoLuong) })
                .ToDictionaryAsync(x => x.Key, x => x.Sl);
            return View(hd);
        }

        [HttpPost]
        public async Task<IActionResult> Create(int maHd, string lyDo, int[] maCthd, int[] soLuong, string[] hinhThuc, int?[] maBtDoi, int[] nhapLai)
        {
            var dongs = new List<BanHangService.DongDoiTra>();
            for (int i = 0; i < maCthd.Length; i++)
            {
                int sl = i < soLuong.Length ? soLuong[i] : 0;
                if (sl <= 0) continue;
                dongs.Add(new BanHangService.DongDoiTra(maCthd[i], sl,
                    i < hinhThuc.Length ? hinhThuc[i] : CtDoiTra.Tra,
                    i < maBtDoi.Length ? maBtDoi[i] : null,
                    nhapLai.Contains(maCthd[i])));
            }
            try
            {
                int id = await _banHang.LapPhieuDoiTraAsync(maHd, MaNvHienTai, lyDo, dongs);
                ThongBao($"Đã lập phiếu đổi trả DT{id}, tồn kho đã được điều chỉnh.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (NghiepVuException ex)
            {
                Loi(ex.Message);
                return RedirectToAction(nameof(Create), new { maHd });
            }
        }
    }
}
