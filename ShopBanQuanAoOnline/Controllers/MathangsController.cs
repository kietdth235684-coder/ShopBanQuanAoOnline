using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>
    /// Module 1 - Quản lý sản phẩm: thêm/sửa/xóa, hình ảnh, giá, danh mục, biến thể size/màu, tìm kiếm - lọc.
    /// Nhân viên được xem; thêm/sửa/xóa dành cho chủ cửa hàng. Tồn kho KHÔNG sửa trực tiếp ở đây mà qua phiếu nhập/xuất/kiểm kê.
    /// </summary>
    public class MathangsController : AdminController
    {
        private static readonly string[] DuoiAnhHopLe = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long KichThuocAnhToiDa = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public MathangsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Mathangs  (tìm theo tên/mã, lọc danh mục, khoảng giá, trạng thái)
        public async Task<IActionResult> Index(string? tukhoa, int? maDm, int? giaTu, int? giaDen, bool? dangBan)
        {
            var q = _context.Mathangs.AsNoTracking().Include(m => m.MaDmNavigation).Include(m => m.BienThes).AsQueryable();
            if (!string.IsNullOrWhiteSpace(tukhoa))
            {
                var tk = tukhoa.Trim();
                q = int.TryParse(tk, out var ma) ? q.Where(m => m.MaMh == ma || m.Ten.Contains(tk)) : q.Where(m => m.Ten.Contains(tk));
            }
            if (maDm.HasValue) q = q.Where(m => m.MaDm == maDm);
            if (giaTu.HasValue) q = q.Where(m => m.GiaBan >= giaTu);
            if (giaDen.HasValue) q = q.Where(m => m.GiaBan <= giaDen);
            if (dangBan.HasValue) q = q.Where(m => m.TrangThai == dangBan);

            ViewBag.MaDmList = new SelectList(await _context.Danhmucs.OrderBy(d => d.Ten).ToListAsync(), "MaDm", "Ten", maDm);
            ViewBag.tukhoa = tukhoa; ViewBag.giaTu = giaTu; ViewBag.giaDen = giaDen; ViewBag.dangBan = dangBan;
            return View(await q.OrderByDescending(m => m.MaMh).ToListAsync());
        }

        // GET: Mathangs/Details/5  (kèm quản lý biến thể size/màu)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var mathang = await _context.Mathangs.AsNoTracking()
                .Include(m => m.MaDmNavigation)
                .Include(m => m.BienThes)
                .FirstOrDefaultAsync(m => m.MaMh == id);
            if (mathang == null) return NotFound();

            var dg = await _context.DanhGias.Where(d => d.MaMh == id && d.TrangThai == HangSo.TrangThaiDanhGia.HienThi)
                .GroupBy(d => 1).Select(g => new { Tb = g.Average(x => x.SoSao), Sl = g.Count() }).FirstOrDefaultAsync();
            ViewBag.DiemTB = dg?.Tb ?? 0;
            ViewBag.SoDanhGia = dg?.Sl ?? 0;
            return View(mathang);
        }

        private async Task<string?> LuuAnhAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!DuoiAnhHopLe.Contains(ext))
            {
                ModelState.AddModelError("file", "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp, .gif");
                return null;
            }
            if (file.Length > KichThuocAnhToiDa)
            {
                ModelState.AddModelError("file", "Ảnh tối đa 5MB");
                return null;
            }
            // Tên file sinh ngẫu nhiên, chỉ ký tự ASCII -> không trùng, không lỗi đường dẫn
            var ten = Guid.NewGuid().ToString("N") + ext;
            var thuMuc = Path.Combine(_env.WebRootPath, "images");
            Directory.CreateDirectory(thuMuc);
            await using var stream = new FileStream(Path.Combine(thuMuc, ten), FileMode.Create);
            await file.CopyToAsync(stream);
            return ten;
        }

        [Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public IActionResult Create()
        {
            ViewBag.MaDmList = new SelectList(_context.Danhmucs.OrderBy(d => d.Ten), "MaDm", "Ten");
            return View(new Mathang { TrangThai = true });
        }

        [HttpPost, Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> Create(IFormFile? file, string? kichCos, string? mauSacs,
            [Bind("Ten,MoTa,GiaGoc,GiaBan,GiaNhap,MaDm,TrangThai")] Mathang mathang)
        {
            if (!await _context.Danhmucs.AnyAsync(d => d.MaDm == mathang.MaDm))
                ModelState.AddModelError("MaDm", "Vui lòng chọn danh mục");
            if (mathang.GiaBan == null) ModelState.AddModelError("GiaBan", "Vui lòng nhập giá bán");
            if (mathang.GiaGoc.HasValue && mathang.GiaBan.HasValue && mathang.GiaGoc < mathang.GiaBan)
                ModelState.AddModelError("GiaGoc", "Giá niêm yết phải lớn hơn hoặc bằng giá bán (để trống nếu không giảm giá)");
            mathang.HinhAnh = await LuuAnhAsync(file);

            if (ModelState.IsValid)
            {
                mathang.SoLuong = 0;
                mathang.LuotXem = 0;
                mathang.LuotMua = 0;
                // Tạo nhanh biến thể từ danh sách size x màu (vd: "S, M, L" và "Đen, Trắng"); tồn ban đầu = 0
                var sizes = TachDanhSach(kichCos);
                var maus = TachDanhSach(mauSacs);
                if (sizes.Count == 0) sizes.Add("Freesize");
                if (maus.Count == 0) maus.Add("Mặc định");
                foreach (var s in sizes)
                    foreach (var c in maus)
                        mathang.BienThes.Add(new BienThe { KichCo = s, MauSac = c, SoLuongTon = 0, TrangThai = true });

                _context.Add(mathang);
                await _context.SaveChangesAsync();
                ThongBao($"Đã thêm sản phẩm \"{mathang.Ten}\" với {mathang.BienThes.Count} biến thể. Hãy lập phiếu nhập để có tồn kho.");
                return RedirectToAction(nameof(Details), new { id = mathang.MaMh });
            }
            ViewBag.MaDmList = new SelectList(_context.Danhmucs.OrderBy(d => d.Ten), "MaDm", "Ten", mathang.MaDm);
            ViewBag.kichCos = kichCos; ViewBag.mauSacs = mauSacs;
            return View(mathang);
        }

        private static List<string> TachDanhSach(string? s) =>
            (s ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(x => x.Length <= 30).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        [Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var mathang = await _context.Mathangs.FindAsync(id);
            if (mathang == null) return NotFound();
            ViewBag.MaDmList = new SelectList(_context.Danhmucs.OrderBy(d => d.Ten), "MaDm", "Ten", mathang.MaDm);
            return View(mathang);
        }

        [HttpPost, Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> Edit(int id, IFormFile? file, [Bind("MaMh,Ten,GiaGoc,GiaBan,GiaNhap,MoTa,MaDm,TrangThai")] Mathang form)
        {
            if (id != form.MaMh) return NotFound();
            var mathang = await _context.Mathangs.FindAsync(id);
            if (mathang == null) return NotFound();
            if (!await _context.Danhmucs.AnyAsync(d => d.MaDm == form.MaDm))
                ModelState.AddModelError("MaDm", "Vui lòng chọn danh mục");
            if (form.GiaBan == null) ModelState.AddModelError("GiaBan", "Vui lòng nhập giá bán");
            if (form.GiaGoc.HasValue && form.GiaBan.HasValue && form.GiaGoc < form.GiaBan)
                ModelState.AddModelError("GiaGoc", "Giá niêm yết phải lớn hơn hoặc bằng giá bán");
            var anhMoi = await LuuAnhAsync(file);

            if (!ModelState.IsValid)
            {
                ViewBag.MaDmList = new SelectList(_context.Danhmucs.OrderBy(d => d.Ten), "MaDm", "Ten", form.MaDm);
                form.HinhAnh = mathang.HinhAnh;
                return View(form);
            }
            // Chỉ cập nhật các trường được phép sửa; SoLuong/LuotXem/LuotMua do hệ thống quản lý
            mathang.Ten = form.Ten;
            mathang.GiaGoc = form.GiaGoc;
            mathang.GiaBan = form.GiaBan;
            mathang.GiaNhap = form.GiaNhap;
            mathang.MoTa = form.MoTa;
            mathang.MaDm = form.MaDm;
            mathang.TrangThai = form.TrangThai;
            if (anhMoi != null) mathang.HinhAnh = anhMoi;
            await _context.SaveChangesAsync();
            ThongBao("Đã cập nhật sản phẩm.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var mathang = await _context.Mathangs.Include(m => m.MaDmNavigation).FirstOrDefaultAsync(m => m.MaMh == id);
            if (mathang == null) return NotFound();
            ViewBag.CoGiaoDich = await CoGiaoDichAsync(mathang.MaMh);
            return View(mathang);
        }

        /// <summary>Sản phẩm đã phát sinh nhập/bán/đặt hàng/đánh giá thì không được xóa (giữ lịch sử), chỉ được ngừng bán.</summary>
        private async Task<bool> CoGiaoDichAsync(int maMh) =>
            await _context.Cthoadons.AnyAsync(c => c.MaMh == maMh)
            || await _context.TheKhos.AnyAsync(t => t.MaBtNavigation!.MaMh == maMh && t.Loai != HangSo.LoaiTheKho.KhoiTao)
            || await _context.CtDonHangs.AnyAsync(c => c.MaBtNavigation!.MaMh == maMh)
            || await _context.DanhGias.AnyAsync(d => d.MaMh == maMh);

        [HttpPost, ActionName("Delete"), Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var mathang = await _context.Mathangs.Include(m => m.BienThes).FirstOrDefaultAsync(m => m.MaMh == id);
            if (mathang == null) return RedirectToAction(nameof(Index));
            if (await CoGiaoDichAsync(id))
            {
                mathang.TrangThai = false;
                await _context.SaveChangesAsync();
                ThongBao("Sản phẩm đã có giao dịch nên không xóa được — đã chuyển sang trạng thái NGỪNG BÁN.");
                return RedirectToAction(nameof(Index));
            }
            var ids = mathang.BienThes.Select(b => b.MaBt).ToList();
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.TheKhos.Where(t => ids.Contains(t.MaBt)).ExecuteDeleteAsync();
            _context.BienThes.RemoveRange(mathang.BienThes);
            _context.Mathangs.Remove(mathang);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            ThongBao("Đã xóa sản phẩm.");
            return RedirectToAction(nameof(Index));
        }

        // ================= BIẾN THỂ SIZE / MÀU =================
        [HttpPost, Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> ThemBienThe(int maMh, string kichCo, string mauSac)
        {
            kichCo = (kichCo ?? "").Trim();
            mauSac = (mauSac ?? "").Trim();
            if (kichCo.Length == 0 || mauSac.Length == 0 || kichCo.Length > 20 || mauSac.Length > 30)
                Loi("Kích cỡ (≤ 20 ký tự) và màu sắc (≤ 30 ký tự) không được để trống.");
            else if (!await _context.Mathangs.AnyAsync(m => m.MaMh == maMh))
                return NotFound();
            else if (await _context.BienThes.AnyAsync(b => b.MaMh == maMh && b.KichCo == kichCo && b.MauSac == mauSac))
                Loi($"Biến thể {kichCo} / {mauSac} đã tồn tại.");
            else
            {
                _context.BienThes.Add(new BienThe { MaMh = maMh, KichCo = kichCo, MauSac = mauSac, SoLuongTon = 0, TrangThai = true });
                await _context.SaveChangesAsync();
                ThongBao($"Đã thêm biến thể {kichCo} / {mauSac}. Tồn kho ban đầu = 0, nhập hàng qua Phiếu nhập.");
            }
            return RedirectToAction(nameof(Details), new { id = maMh });
        }

        [HttpPost, Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> DoiTrangThaiBienThe(int maBt)
        {
            var bt = await _context.BienThes.FindAsync(maBt);
            if (bt == null) return NotFound();
            bt.TrangThai = !bt.TrangThai;
            await _context.SaveChangesAsync();
            ThongBao($"Biến thể {bt.TenBienThe}: {(bt.TrangThai ? "đang bán" : "ngừng bán")}.");
            return RedirectToAction(nameof(Details), new { id = bt.MaMh });
        }

        [HttpPost, Authorize(Policy = PhanQuyen.ChuCuaHang)]
        public async Task<IActionResult> XoaBienThe(int maBt)
        {
            var bt = await _context.BienThes.FindAsync(maBt);
            if (bt == null) return NotFound();
            bool daDung = bt.SoLuongTon > 0
                || await _context.TheKhos.AnyAsync(t => t.MaBt == maBt && t.Loai != HangSo.LoaiTheKho.KhoiTao)
                || await _context.Cthoadons.AnyAsync(c => c.MaBt == maBt)
                || await _context.CtDonHangs.AnyAsync(c => c.MaBt == maBt);
            if (daDung)
            {
                Loi("Biến thể còn tồn kho hoặc đã có giao dịch nên không xóa được. Hãy chuyển sang ngừng bán.");
            }
            else
            {
                await _context.TheKhos.Where(t => t.MaBt == maBt).ExecuteDeleteAsync();
                _context.BienThes.Remove(bt);
                await _context.SaveChangesAsync();
                ThongBao("Đã xóa biến thể.");
            }
            return RedirectToAction(nameof(Details), new { id = bt.MaMh });
        }
    }
}
