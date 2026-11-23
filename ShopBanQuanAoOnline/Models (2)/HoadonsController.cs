using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>
    /// Module 3 - Tra cứu hóa đơn bán hàng nội bộ. Hóa đơn chỉ được sinh ra bởi Bán tại quầy hoặc khi đơn COD hoàn tất,
    /// không cho thêm/sửa/xóa thủ công để đảm bảo doanh thu và tồn kho nhất quán.
    /// </summary>
    public class HoadonsController : AdminController
    {
        private readonly ApplicationDbContext _context;
        public HoadonsController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index(string? tukhoa, int? loai, DateTime? tuNgay, DateTime? denNgay)
        {
            var q = _context.Hoadons.AsNoTracking().Include(h => h.MaKhNavigation).Include(h => h.MaNvNavigation).AsQueryable();
            if (!string.IsNullOrWhiteSpace(tukhoa))
            {
                var tk = tukhoa.Trim();
                var so = tk.TrimStart('H', 'D', 'h', 'd');
                q = int.TryParse(so, out var ma) && so.Length < 8
                    ? q.Where(h => h.MaHd == ma || h.MaDh == ma)
                    : q.Where(h => h.MaKhNavigation != null && (h.MaKhNavigation.DienThoai!.Contains(tk) || h.MaKhNavigation.Ten.Contains(tk)));
            }
            if (loai.HasValue) q = q.Where(h => h.LoaiHd == loai);
            if (tuNgay.HasValue) q = q.Where(h => h.Ngay >= tuNgay.Value.Date);
            if (denNgay.HasValue) q = q.Where(h => h.Ngay < denNgay.Value.Date.AddDays(1));
            ViewBag.tukhoa = tukhoa; ViewBag.Loai = loai;
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd"); ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            return View(await q.OrderByDescending(h => h.Ngay).Take(300).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var hoadon = await _context.Hoadons.AsNoTracking()
                .Include(h => h.MaKhNavigation).Include(h => h.MaNvNavigation).Include(h => h.MaDhNavigation)
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaMhNavigation)
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaBtNavigation)
                .FirstOrDefaultAsync(m => m.MaHd == id);
            if (hoadon == null) return NotFound();
            ViewBag.CuaHang = await _context.Cuahangs.AsNoTracking().FirstOrDefaultAsync();
            ViewBag.DoiTras = await _context.PhieuDoiTras.AsNoTracking().Where(p => p.MaHd == id).OrderBy(p => p.NgayLap).ToListAsync();
            return View(hoadon);
        }
    }
}
