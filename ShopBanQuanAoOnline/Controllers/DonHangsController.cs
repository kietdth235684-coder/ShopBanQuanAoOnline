using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;
using ShopBanQuanAoOnline.Services;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Module 3 - Tiếp nhận và xử lý đơn COD do website chuyển đến.</summary>
    public class DonHangsController : AdminController
    {
        private readonly ApplicationDbContext _db;
        private readonly DonHangService _don;

        public DonHangsController(ApplicationDbContext db, DonHangService don)
        {
            _db = db;
            _don = don;
        }

        public async Task<IActionResult> Index(int? trangThai, string? tukhoa, DateTime? tuNgay, DateTime? denNgay)
        {
            var q = _db.DonHangs.AsNoTracking().Include(d => d.MaKhNavigation).AsQueryable();
            if (trangThai.HasValue) q = q.Where(d => d.TrangThai == trangThai);
            if (!string.IsNullOrWhiteSpace(tukhoa))
            {
                var tk = tukhoa.Trim().TrimStart('D', 'H', 'd', 'h');
                q = int.TryParse(tk, out var ma)
                    ? q.Where(d => d.MaDh == ma || d.DienThoaiNhan.Contains(tukhoa.Trim()))
                    : q.Where(d => d.DienThoaiNhan.Contains(tukhoa) || d.HoTenNhan.Contains(tukhoa));
            }
            if (tuNgay.HasValue) q = q.Where(d => d.NgayDat >= tuNgay.Value.Date);
            if (denNgay.HasValue) q = q.Where(d => d.NgayDat < denNgay.Value.Date.AddDays(1));

            ViewBag.DemTheoTrangThai = await _db.DonHangs.GroupBy(d => d.TrangThai).Select(g => new { g.Key, Sl = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Sl);
            ViewBag.TrangThai = trangThai; ViewBag.tukhoa = tukhoa;
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd"); ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            return View(await q.OrderBy(d => d.TrangThai == HangSo.TrangThaiDon.ChoXacNhan ? 0 : 1).ThenByDescending(d => d.NgayDat).Take(300).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var don = await _db.DonHangs.AsNoTracking()
                .Include(d => d.MaKhNavigation).Include(d => d.MaNvNavigation)
                .Include(d => d.CtDonHangs).ThenInclude(c => c.MaBtNavigation).ThenInclude(b => b!.MaMhNavigation)
                .FirstOrDefaultAsync(d => d.MaDh == id);
            if (don == null) return NotFound();
            ViewBag.MaHd = await _db.Hoadons.Where(h => h.MaDh == id).Select(h => (int?)h.MaHd).FirstOrDefaultAsync();
            return View(don);
        }

        private async Task<IActionResult> ThucHien(int id, Func<Task> hanhDong, string thanhCong)
        {
            try
            {
                await hanhDong();
                ThongBao(thanhCong);
            }
            catch (NghiepVuException ex)
            {
                Loi(ex.Message);
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public Task<IActionResult> XacNhan(int id) =>
            ThucHien(id, () => _don.XacNhanAsync(id, MaNvHienTai), "Đã xác nhận đơn và trừ tồn kho.");

        [HttpPost]
        public Task<IActionResult> GiaoHang(int id) =>
            ThucHien(id, () => _don.BatDauGiaoAsync(id, MaNvHienTai), "Đơn đã chuyển sang Đang giao hàng.");

        [HttpPost]
        public async Task<IActionResult> GiaoThanhCong(int id)
        {
            try
            {
                int maHd = await _don.GiaoThanhCongAsync(id, MaNvHienTai);
                ThongBao($"Đơn hoàn tất, đã thu tiền COD. Hóa đơn nội bộ HD{maHd} đã được tạo.");
            }
            catch (NghiepVuException ex) { Loi(ex.Message); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public Task<IActionResult> GiaoThatBai(int id, string? lyDo) =>
            ThucHien(id, () => _don.GiaoThatBaiAsync(id, MaNvHienTai, lyDo), "Đã ghi nhận giao thất bại, hàng đã được hoàn về kho. Không tạo hóa đơn.");

        [HttpPost]
        public Task<IActionResult> Huy(int id, string? lyDo) =>
            ThucHien(id, () => _don.NhanVienHuyAsync(id, MaNvHienTai, lyDo), "Đã hủy đơn hàng.");
    }
}
