using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;
using ShopBanQuanAoOnline.Services;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Danh sách biến thể dùng cho các form phiếu kho.</summary>
    internal static class DanhSachBienThe
    {
        public static async Task<List<BienThe>> LayAsync(ApplicationDbContext db) =>
            await db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation)
                .Where(b => b.TrangThai)
                .OrderBy(b => b.MaMhNavigation!.Ten).ThenBy(b => b.KichCo).ThenBy(b => b.MauSac)
                .ToListAsync();

        public static List<KhoService.DongPhieu> Gom(int[]? maBt, int[]? soLuong, int[]? donGia)
        {
            var kq = new List<KhoService.DongPhieu>();
            if (maBt == null || soLuong == null) return kq;
            for (int i = 0; i < maBt.Length && i < soLuong.Length; i++)
                if (maBt[i] > 0 && soLuong[i] != 0)
                    kq.Add(new KhoService.DongPhieu(maBt[i], soLuong[i], donGia != null && i < donGia.Length ? donGia[i] : 0));
            return kq;
        }
    }

    /// <summary>Module 2 - Phiếu nhập hàng (lưu lịch sử nhập hàng). Phiếu đã lập không sửa/xóa; sai sót điều chỉnh bằng phiếu xuất hoặc kiểm kê.</summary>
    public class PhieuNhapsController : AdminController
    {
        private readonly ApplicationDbContext _db;
        private readonly KhoService _kho;
        public PhieuNhapsController(ApplicationDbContext db, KhoService kho) { _db = db; _kho = kho; }

        public async Task<IActionResult> Index(DateTime? tuNgay, DateTime? denNgay, int? maNcc)
        {
            var q = _db.PhieuNhaps.AsNoTracking().Include(p => p.MaNccNavigation).Include(p => p.MaNvNavigation).Include(p => p.CtPhieuNhaps).AsQueryable();
            if (tuNgay.HasValue) q = q.Where(p => p.NgayNhap >= tuNgay.Value.Date);
            if (denNgay.HasValue) q = q.Where(p => p.NgayNhap < denNgay.Value.Date.AddDays(1));
            if (maNcc.HasValue) q = q.Where(p => p.MaNcc == maNcc);
            ViewBag.NCC = new SelectList(await _db.NhaCungCaps.OrderBy(n => n.Ten).ToListAsync(), "MaNcc", "Ten", maNcc);
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd"); ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            return View(await q.OrderByDescending(p => p.NgayNhap).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.PhieuNhaps.AsNoTracking().Include(x => x.MaNccNavigation).Include(x => x.MaNvNavigation)
                .Include(x => x.CtPhieuNhaps).ThenInclude(c => c.MaBtNavigation).ThenInclude(b => b!.MaMhNavigation)
                .FirstOrDefaultAsync(x => x.MaPn == id);
            return p == null ? NotFound() : View(p);
        }

        public async Task<IActionResult> Create(int? maNcc)
        {
            await NapAsync(maNcc);
            return View();
        }

        private async Task NapAsync(int? maNcc)
        {
            ViewBag.NCC = new SelectList(await _db.NhaCungCaps.Where(n => n.TrangThai).OrderBy(n => n.Ten).ToListAsync(), "MaNcc", "Ten", maNcc);
            ViewBag.BienThes = await DanhSachBienThe.LayAsync(_db);
        }

        [HttpPost]
        public async Task<IActionResult> Create(int maNcc, string? ghiChu, int[] maBt, int[] soLuong, int[] donGia)
        {
            try
            {
                int id = await _kho.LapPhieuNhapAsync(maNcc, MaNvHienTai, ghiChu, DanhSachBienThe.Gom(maBt, soLuong, donGia));
                ThongBao($"Đã lập phiếu nhập PN{id} và cập nhật tồn kho.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (NghiepVuException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await NapAsync(maNcc);
                return View();
            }
        }
    }

    /// <summary>Module 2 - Phiếu xuất kho (hàng lỗi, trả nhà cung cấp, sử dụng nội bộ...). Không dùng cho bán hàng.</summary>
    public class PhieuXuatsController : AdminController
    {
        public static readonly string[] LyDoMau = { "Hàng lỗi / hư hỏng", "Trả lại nhà cung cấp", "Sử dụng nội bộ / trưng bày", "Khác" };
        private readonly ApplicationDbContext _db;
        private readonly KhoService _kho;
        public PhieuXuatsController(ApplicationDbContext db, KhoService kho) { _db = db; _kho = kho; }

        public async Task<IActionResult> Index()
        {
            return View(await _db.PhieuXuats.AsNoTracking().Include(p => p.MaNvNavigation).Include(p => p.CtPhieuXuats)
                .OrderByDescending(p => p.NgayXuat).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.PhieuXuats.AsNoTracking().Include(x => x.MaNvNavigation)
                .Include(x => x.CtPhieuXuats).ThenInclude(c => c.MaBtNavigation).ThenInclude(b => b!.MaMhNavigation)
                .FirstOrDefaultAsync(x => x.MaPx == id);
            return p == null ? NotFound() : View(p);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.BienThes = await DanhSachBienThe.LayAsync(_db);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(string lyDo, string? ghiChu, int[] maBt, int[] soLuong)
        {
            try
            {
                int id = await _kho.LapPhieuXuatAsync(MaNvHienTai, lyDo, ghiChu, DanhSachBienThe.Gom(maBt, soLuong, null));
                ThongBao($"Đã lập phiếu xuất PX{id} và trừ tồn kho.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (NghiepVuException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.BienThes = await DanhSachBienThe.LayAsync(_db);
                return View();
            }
        }
    }

    /// <summary>Module 2 - Kiểm kê và điều chỉnh tồn kho theo số đếm thực tế.</summary>
    public class KiemKesController : AdminController
    {
        private readonly ApplicationDbContext _db;
        private readonly KhoService _kho;
        public KiemKesController(ApplicationDbContext db, KhoService kho) { _db = db; _kho = kho; }

        public async Task<IActionResult> Index()
        {
            return View(await _db.PhieuKiemKes.AsNoTracking().Include(p => p.MaNvNavigation).Include(p => p.CtKiemKes)
                .OrderByDescending(p => p.NgayKiemKe).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.PhieuKiemKes.AsNoTracking().Include(x => x.MaNvNavigation)
                .Include(x => x.CtKiemKes).ThenInclude(c => c.MaBtNavigation).ThenInclude(b => b!.MaMhNavigation)
                .FirstOrDefaultAsync(x => x.MaKk == id);
            return p == null ? NotFound() : View(p);
        }

        /// <summary>Form kiểm kê: liệt kê biến thể (lọc theo danh mục), nhập số lượng đếm thực tế.</summary>
        public async Task<IActionResult> Create(int? maDm)
        {
            ViewBag.DanhMuc = new SelectList(await _db.Danhmucs.OrderBy(d => d.Ten).ToListAsync(), "MaDm", "Ten", maDm);
            ViewBag.MaDm = maDm;
            var ds = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation)
                .Where(b => maDm == null || b.MaMhNavigation!.MaDm == maDm)
                .OrderBy(b => b.MaMhNavigation!.Ten).ThenBy(b => b.KichCo).ThenBy(b => b.MauSac).ToListAsync();
            return View(ds);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string? ghiChu, int[] maBt, int?[] thucTe, int? maDm)
        {
            var dict = new Dictionary<int, int>();
            for (int i = 0; i < maBt.Length && i < thucTe.Length; i++)
                if (thucTe[i].HasValue) dict[maBt[i]] = thucTe[i]!.Value;
            try
            {
                int id = await _kho.LapPhieuKiemKeAsync(MaNvHienTai, ghiChu, dict);
                ThongBao($"Đã lưu phiếu kiểm kê KK{id}; tồn kho đã được điều chỉnh theo số thực tế.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (NghiepVuException ex)
            {
                Loi(ex.Message);
                return RedirectToAction(nameof(Create), new { maDm });
            }
        }
    }

    /// <summary>Module 2 - Theo dõi tồn kho, cảnh báo tồn thấp, thẻ kho từng biến thể, báo cáo nhập - xuất - tồn.</summary>
    public class KhosController : AdminController
    {
        private readonly ApplicationDbContext _db;
        private readonly KhoService _kho;
        private readonly BaoCaoService _baoCao;
        public KhosController(ApplicationDbContext db, KhoService kho, BaoCaoService baoCao) { _db = db; _kho = kho; _baoCao = baoCao; }

        public async Task<IActionResult> Index(string? tukhoa, int? maDm, bool tonThap = false)
        {
            int nguong = _kho.NguongTonThap;
            var q = _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).ThenInclude(m => m!.MaDmNavigation).AsQueryable();
            if (!string.IsNullOrWhiteSpace(tukhoa)) q = q.Where(b => b.MaMhNavigation!.Ten.Contains(tukhoa) || b.MauSac.Contains(tukhoa) || b.KichCo == tukhoa);
            if (maDm.HasValue) q = q.Where(b => b.MaMhNavigation!.MaDm == maDm);
            if (tonThap) q = q.Where(b => b.TrangThai && b.SoLuongTon <= nguong);
            ViewBag.DanhMuc = new SelectList(await _db.Danhmucs.OrderBy(d => d.Ten).ToListAsync(), "MaDm", "Ten", maDm);
            ViewBag.tukhoa = tukhoa; ViewBag.TonThap = tonThap; ViewBag.Nguong = nguong;
            return View(await q.OrderBy(b => b.SoLuongTon > nguong).ThenBy(b => b.MaMhNavigation!.Ten).ThenBy(b => b.KichCo).ToListAsync());
        }

        public async Task<IActionResult> TheKho(int id)
        {
            var bt = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).FirstOrDefaultAsync(b => b.MaBt == id);
            if (bt == null) return NotFound();
            ViewBag.BienThe = bt;
            return View(await _db.TheKhos.AsNoTracking().Include(t => t.MaNvNavigation).Where(t => t.MaBt == id)
                .OrderByDescending(t => t.Ngay).ThenByDescending(t => t.MaTk).ToListAsync());
        }

        public async Task<IActionResult> NhapXuatTon(DateTime? tuNgay, DateTime? denNgay, int? maDm)
        {
            var den = denNgay ?? DateTime.Today;
            var tu = tuNgay ?? new DateTime(den.Year, den.Month, 1);
            if (tu > den) (tu, den) = (den, tu);
            ViewBag.TuNgay = tu.ToString("yyyy-MM-dd"); ViewBag.DenNgay = den.ToString("yyyy-MM-dd");
            ViewBag.DanhMuc = new SelectList(await _db.Danhmucs.OrderBy(d => d.Ten).ToListAsync(), "MaDm", "Ten", maDm);
            return View(await _baoCao.NhapXuatTonAsync(tu, den, maDm));
        }
    }
}
