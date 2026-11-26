using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Module 2 - Thông tin nhà cung cấp ở mức cơ bản. KHÔNG quản lý công nợ hay thanh toán nhà cung cấp.</summary>
    public class NhaCungCapsController : AdminController
    {
        private readonly ApplicationDbContext _db;
        public NhaCungCapsController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? tukhoa)
        {
            var q = _db.NhaCungCaps.AsNoTracking().Include(n => n.PhieuNhaps).AsQueryable();
            if (!string.IsNullOrWhiteSpace(tukhoa)) q = q.Where(n => n.Ten.Contains(tukhoa) || (n.DienThoai != null && n.DienThoai.Contains(tukhoa)));
            ViewBag.tukhoa = tukhoa;
            return View(await q.OrderBy(n => n.Ten).ToListAsync());
        }

        /// <summary>Chi tiết + lịch sử nhập hàng từ nhà cung cấp.</summary>
        public async Task<IActionResult> Details(int id)
        {
            var ncc = await _db.NhaCungCaps.AsNoTracking()
                .Include(n => n.PhieuNhaps).ThenInclude(p => p.MaNvNavigation)
                .FirstOrDefaultAsync(n => n.MaNcc == id);
            return ncc == null ? NotFound() : View(ncc);
        }

        public IActionResult Create() => View("Form", new NhaCungCap { TrangThai = true });

        public async Task<IActionResult> Edit(int id)
        {
            var ncc = await _db.NhaCungCaps.FindAsync(id);
            return ncc == null ? NotFound() : View("Form", ncc);
        }

        [HttpPost]
        public async Task<IActionResult> Save([Bind("MaNcc,Ten,DienThoai,Email,DiaChi,GhiChu,TrangThai")] NhaCungCap ncc)
        {
            ModelState.Remove(nameof(NhaCungCap.PhieuNhaps));
            if (!ModelState.IsValid) return View("Form", ncc);
            if (ncc.MaNcc == 0) _db.NhaCungCaps.Add(ncc);
            else
            {
                if (!await _db.NhaCungCaps.AnyAsync(n => n.MaNcc == ncc.MaNcc)) return NotFound();
                _db.NhaCungCaps.Update(ncc);
            }
            await _db.SaveChangesAsync();
            ThongBao("Đã lưu nhà cung cấp.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var ncc = await _db.NhaCungCaps.FindAsync(id);
            if (ncc == null) return RedirectToAction(nameof(Index));
            if (await _db.PhieuNhaps.AnyAsync(p => p.MaNcc == id))
            {
                ncc.TrangThai = false;
                ThongBao("Nhà cung cấp đã có lịch sử nhập hàng nên không xóa — đã chuyển sang ngừng hợp tác.");
            }
            else
            {
                _db.NhaCungCaps.Remove(ncc);
                ThongBao("Đã xóa nhà cung cấp.");
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
