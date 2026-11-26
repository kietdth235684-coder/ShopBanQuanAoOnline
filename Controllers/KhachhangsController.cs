using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>
    /// Module 4 (phía quản trị) - Tra cứu hồ sơ khách hàng, lịch sử đơn hàng / hóa đơn.
    /// Tài khoản khách do khách tự đăng ký trên website; quản trị không xem/đặt mật khẩu của khách.
    /// </summary>
    public class KhachhangsController : AdminController
    {
        private readonly ApplicationDbContext _context;
        public KhachhangsController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index(string? tukhoa)
        {
            var q = _context.Khachhangs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(tukhoa))
                q = q.Where(k => k.Ten.Contains(tukhoa) || (k.Email != null && k.Email.Contains(tukhoa)) || (k.DienThoai != null && k.DienThoai.Contains(tukhoa)));
            ViewBag.tukhoa = tukhoa;
            var ds = await q.OrderByDescending(k => k.MaKh).Select(k => new
            {
                Kh = k,
                SoDon = k.DonHangs.Count,
                DaMua = k.Hoadons.Where(h => h.TrangThai == HangSo.TrangThaiHoaDon.HoanTat).Sum(h => (long?)h.TongTien) ?? 0
            }).Take(300).ToListAsync();
            ViewBag.SoDon = ds.ToDictionary(x => x.Kh.MaKh, x => x.SoDon);
            ViewBag.DaMua = ds.ToDictionary(x => x.Kh.MaKh, x => x.DaMua);
            return View(ds.Select(x => x.Kh).ToList());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var kh = await _context.Khachhangs.AsNoTracking()
                .Include(k => k.Diachis)
                .Include(k => k.DonHangs)
                .Include(k => k.Hoadons)
                .FirstOrDefaultAsync(m => m.MaKh == id);
            return kh == null ? NotFound() : View(kh);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var kh = await _context.Khachhangs.FindAsync(id);
            return kh == null ? NotFound() : View(kh);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [Bind("MaKh,Ten,DienThoai,Email")] Khachhang form)
        {
            if (id != form.MaKh) return NotFound();
            var kh = await _context.Khachhangs.FindAsync(id);
            if (kh == null) return NotFound();
            var email = form.Email?.Trim();
            if (string.IsNullOrEmpty(email)) ModelState.AddModelError("Email", "Email không được để trống (dùng để đăng nhập).");
            else if (await _context.Khachhangs.AnyAsync(k => k.Email == email && k.MaKh != id))
                ModelState.AddModelError("Email", "Email đã được dùng cho tài khoản khác.");
            if (!ModelState.IsValid) return View(form);

            kh.Ten = form.Ten;
            kh.DienThoai = form.DienThoai;
            kh.Email = email;
            await _context.SaveChangesAsync();
            ThongBao("Đã cập nhật thông tin khách hàng.");
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var kh = await _context.Khachhangs.FirstOrDefaultAsync(m => m.MaKh == id);
            if (kh == null) return NotFound();
            ViewBag.CoGiaoDich = await CoGiaoDichAsync(kh.MaKh);
            return View(kh);
        }

        private async Task<bool> CoGiaoDichAsync(int maKh) =>
            await _context.DonHangs.AnyAsync(d => d.MaKh == maKh)
            || await _context.Hoadons.AnyAsync(h => h.MaKh == maKh)
            || await _context.DanhGias.AnyAsync(d => d.MaKh == maKh);

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!LaChuCuaHang) return Forbid();
            if (await CoGiaoDichAsync(id))
            {
                Loi("Khách hàng đã có đơn hàng / hóa đơn / đánh giá nên không thể xóa (giữ lịch sử giao dịch).");
                return RedirectToAction(nameof(Details), new { id });
            }
            var kh = await _context.Khachhangs.Include(k => k.Diachis).FirstOrDefaultAsync(k => k.MaKh == id);
            if (kh != null)
            {
                _context.Diachis.RemoveRange(kh.Diachis);
                _context.Khachhangs.Remove(kh);
                await _context.SaveChangesAsync();
                ThongBao("Đã xóa khách hàng.");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
