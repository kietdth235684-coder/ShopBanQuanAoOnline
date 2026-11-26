using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Module 6 - Chấm công cơ bản: vào ca / kết thúc ca mỗi ngày, xem tổng ngày công và giờ làm theo tháng. Không tính lương.</summary>
    public class ChamCongsController : AdminController
    {
        private readonly ApplicationDbContext _db;
        public ChamCongsController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(int? thang, int? nam, int? maNv)
        {
            var t = thang is >= 1 and <= 12 ? thang.Value : DateTime.Today.Month;
            var n = nam is >= 2000 and <= 2100 ? nam.Value : DateTime.Today.Year;
            var tu = new DateTime(n, t, 1);
            var den = tu.AddMonths(1);
            // Nhân viên chỉ xem được chấm công của chính mình
            if (!LaChuCuaHang) maNv = MaNvHienTai;

            var q = _db.ChamCongs.AsNoTracking().Include(c => c.MaNvNavigation).Where(c => c.Ngay >= tu && c.Ngay < den);
            if (maNv.HasValue) q = q.Where(c => c.MaNv == maNv);
            var ds = await q.OrderByDescending(c => c.Ngay).ThenBy(c => c.MaNvNavigation!.Ten).ToListAsync();

            ViewBag.Thang = t; ViewBag.Nam = n; ViewBag.MaNvChon = maNv;
            if (LaChuCuaHang) ViewBag.NhanViens = new SelectList(await _db.Nhanviens.OrderBy(x => x.Ten).ToListAsync(), "MaNv", "Ten", maNv);
            int ma = MaNvHienTai;
            var homNay = DateTime.Today;
            ViewBag.HomNay = await _db.ChamCongs.AsNoTracking().FirstOrDefaultAsync(c => c.MaNv == ma && c.Ngay == homNay);
            return View(ds);
        }

        [HttpPost]
        public async Task<IActionResult> VaoCa(string? quayVe)
        {
            int ma = MaNvHienTai;
            var homNay = DateTime.Today;
            if (await _db.ChamCongs.AnyAsync(c => c.MaNv == ma && c.Ngay == homNay))
                Loi("Hôm nay bạn đã chấm công vào ca.");
            else
            {
                _db.ChamCongs.Add(new ChamCong { MaNv = ma, Ngay = homNay, GioVao = DateTime.Now });
                try { await _db.SaveChangesAsync(); ThongBao($"Đã vào ca lúc {DateTime.Now:HH:mm}."); }
                catch (DbUpdateException) { Loi("Hôm nay bạn đã chấm công vào ca."); }
            }
            return quayVe == "home" ? RedirectToAction("Index", "Home") : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RaCa(string? quayVe)
        {
            int ma = MaNvHienTai;
            var homNay = DateTime.Today;
            var bayGio = DateTime.Now;
            int rows = await _db.ChamCongs.Where(c => c.MaNv == ma && c.Ngay == homNay && c.GioRa == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.GioRa, bayGio));
            if (rows == 0) Loi("Bạn chưa vào ca hoặc đã kết thúc ca hôm nay.");
            else ThongBao($"Đã kết thúc ca lúc {bayGio:HH:mm}.");
            return quayVe == "home" ? RedirectToAction("Index", "Home") : RedirectToAction(nameof(Index));
        }

        /// <summary>Chủ cửa hàng điều chỉnh khi nhân viên quên chấm công.</summary>
        [Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> Edit(int id)
        {
            var cc = await _db.ChamCongs.Include(c => c.MaNvNavigation).FirstOrDefaultAsync(c => c.MaCc == id);
            return cc == null ? NotFound() : View(cc);
        }

        [HttpPost, Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> Edit(int id, TimeSpan gioVao, TimeSpan? gioRa, string? ghiChu)
        {
            var cc = await _db.ChamCongs.Include(c => c.MaNvNavigation).FirstOrDefaultAsync(c => c.MaCc == id);
            if (cc == null) return NotFound();
            var vao = cc.Ngay.Date + gioVao;
            DateTime? ra = gioRa.HasValue ? cc.Ngay.Date + gioRa.Value : null;
            if (ra.HasValue && ra < vao)
            {
                ModelState.AddModelError("", "Giờ ra phải sau giờ vào.");
                return View(cc);
            }
            cc.GioVao = vao;
            cc.GioRa = ra;
            cc.GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
            await _db.SaveChangesAsync();
            ThongBao("Đã cập nhật chấm công.");
            return RedirectToAction(nameof(Index), new { thang = cc.Ngay.Month, nam = cc.Ngay.Year });
        }
    }
}
