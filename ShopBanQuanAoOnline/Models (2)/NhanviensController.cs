using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Module 6 - Quản lý tài khoản nhân viên và phân quyền (chỉ chủ cửa hàng).</summary>
    [Authorize(Policy = PhanQuyen.ChuCuaHang)]
    public class NhanviensController : AdminController
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Nhanvien> _hasher;

        public NhanviensController(ApplicationDbContext context, IPasswordHasher<Nhanvien> hasher)
        {
            _context = context;
            _hasher = hasher;
        }

        private void NapDanhSach(Nhanvien? nv = null)
        {
            ViewData["MaCv"] = new SelectList(_context.Chucvus.OrderBy(c => c.Ten), "MaCv", "Ten", nv?.MaCv);
            ViewData["VaiTro"] = new SelectList(new[]
            {
                new { Ma = HangSo.VaiTro.NhanVien, Ten = "Nhân viên" },
                new { Ma = HangSo.VaiTro.ChuCuaHang, Ten = "Chủ cửa hàng" }
            }, "Ma", "Ten", nv?.VaiTro);
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Nhanviens.AsNoTracking().Include(n => n.MaCvNavigation).OrderBy(n => n.Ten).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var nv = await _context.Nhanviens.AsNoTracking().Include(n => n.MaCvNavigation).FirstOrDefaultAsync(m => m.MaNv == id);
            if (nv == null) return NotFound();
            var dauThang = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            ViewBag.ChamCong = await _context.ChamCongs.AsNoTracking().Where(c => c.MaNv == id && c.Ngay >= dauThang).OrderByDescending(c => c.Ngay).ToListAsync();
            return View(nv);
        }

        public IActionResult Create()
        {
            NapDanhSach();
            return View(new Nhanvien { TrangThai = true, VaiTro = HangSo.VaiTro.NhanVien });
        }

        [HttpPost]
        public async Task<IActionResult> Create([Bind("Ten,MaCv,DienThoai,Email,VaiTro,TrangThai")] Nhanvien nhanvien, string matKhau)
        {
            await KiemTraAsync(nhanvien);
            if (string.IsNullOrEmpty(matKhau) || matKhau.Length < 6)
                ModelState.AddModelError("", "Mật khẩu tối thiểu 6 ký tự.");
            if (ModelState.IsValid)
            {
                nhanvien.Email = nhanvien.Email!.Trim();
                nhanvien.MatKhau = _hasher.HashPassword(nhanvien, matKhau);
                _context.Add(nhanvien);
                await _context.SaveChangesAsync();
                ThongBao("Đã tạo tài khoản nhân viên.");
                return RedirectToAction(nameof(Index));
            }
            NapDanhSach(nhanvien);
            return View(nhanvien);
        }

        private async Task KiemTraAsync(Nhanvien nv)
        {
            if (nv.VaiTro != HangSo.VaiTro.ChuCuaHang && nv.VaiTro != HangSo.VaiTro.NhanVien)
                ModelState.AddModelError("VaiTro", "Vai trò không hợp lệ.");
            if (!await _context.Chucvus.AnyAsync(c => c.MaCv == nv.MaCv))
                ModelState.AddModelError("MaCv", "Vui lòng chọn chức vụ.");
            var email = nv.Email?.Trim();
            if (string.IsNullOrEmpty(email)) ModelState.AddModelError("Email", "Vui lòng nhập email đăng nhập.");
            else if (await _context.Nhanviens.AnyAsync(n => n.Email == email && n.MaNv != nv.MaNv))
                ModelState.AddModelError("Email", "Email đã được dùng cho tài khoản khác.");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var nv = await _context.Nhanviens.FindAsync(id);
            if (nv == null) return NotFound();
            NapDanhSach(nv);
            return View(nv);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [Bind("MaNv,Ten,MaCv,DienThoai,Email,VaiTro,TrangThai")] Nhanvien form, string? matKhauMoi)
        {
            if (id != form.MaNv) return NotFound();
            var nv = await _context.Nhanviens.FindAsync(id);
            if (nv == null) return NotFound();
            await KiemTraAsync(form);
            if (!string.IsNullOrEmpty(matKhauMoi) && matKhauMoi.Length < 6)
                ModelState.AddModelError("", "Mật khẩu mới tối thiểu 6 ký tự.");
            // Không tự hạ quyền / tự khóa chính mình, và luôn còn ít nhất 1 chủ cửa hàng đang hoạt động
            if (id == MaNvHienTai && (form.VaiTro != HangSo.VaiTro.ChuCuaHang || !form.TrangThai))
                ModelState.AddModelError("", "Không thể tự hạ quyền hoặc tự khóa tài khoản đang đăng nhập.");
            if (nv.VaiTro == HangSo.VaiTro.ChuCuaHang && (form.VaiTro != HangSo.VaiTro.ChuCuaHang || !form.TrangThai)
                && !await _context.Nhanviens.AnyAsync(n => n.MaNv != id && n.VaiTro == HangSo.VaiTro.ChuCuaHang && n.TrangThai))
                ModelState.AddModelError("", "Hệ thống phải còn ít nhất một chủ cửa hàng đang hoạt động.");

            if (!ModelState.IsValid)
            {
                NapDanhSach(form);
                return View(form);
            }
            nv.Ten = form.Ten;
            nv.MaCv = form.MaCv;
            nv.DienThoai = form.DienThoai;
            nv.Email = form.Email!.Trim();
            nv.VaiTro = form.VaiTro;
            nv.TrangThai = form.TrangThai;
            if (!string.IsNullOrEmpty(matKhauMoi)) nv.MatKhau = _hasher.HashPassword(nv, matKhauMoi);
            await _context.SaveChangesAsync();
            ThongBao("Đã cập nhật nhân viên.");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var nv = await _context.Nhanviens.Include(n => n.MaCvNavigation).FirstOrDefaultAsync(m => m.MaNv == id);
            if (nv == null) return NotFound();
            ViewBag.CoLichSu = await CoLichSuAsync(nv.MaNv);
            return View(nv);
        }

        private async Task<bool> CoLichSuAsync(int maNv) =>
            await _context.Hoadons.AnyAsync(h => h.MaNv == maNv)
            || await _context.DonHangs.AnyAsync(d => d.MaNv == maNv)
            || await _context.PhieuNhaps.AnyAsync(p => p.MaNv == maNv)
            || await _context.PhieuXuats.AnyAsync(p => p.MaNv == maNv)
            || await _context.PhieuKiemKes.AnyAsync(p => p.MaNv == maNv)
            || await _context.PhieuDoiTras.AnyAsync(p => p.MaNv == maNv)
            || await _context.TheKhos.AnyAsync(t => t.MaNv == maNv)
            || await _context.ChamCongs.AnyAsync(c => c.MaNv == maNv);

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id == MaNvHienTai)
            {
                Loi("Không thể xóa tài khoản đang đăng nhập.");
                return RedirectToAction(nameof(Index));
            }
            var nv = await _context.Nhanviens.FindAsync(id);
            if (nv == null) return RedirectToAction(nameof(Index));
            if (nv.VaiTro == HangSo.VaiTro.ChuCuaHang
                && !await _context.Nhanviens.AnyAsync(n => n.MaNv != id && n.VaiTro == HangSo.VaiTro.ChuCuaHang && n.TrangThai))
            {
                Loi("Hệ thống phải còn ít nhất một chủ cửa hàng.");
                return RedirectToAction(nameof(Index));
            }
            if (await CoLichSuAsync(id))
            {
                nv.TrangThai = false;
                ThongBao("Nhân viên đã có lịch sử thao tác nên không xóa — tài khoản đã được KHÓA.");
            }
            else
            {
                _context.Nhanviens.Remove(nv);
                ThongBao("Đã xóa tài khoản nhân viên.");
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
