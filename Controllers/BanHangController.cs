using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;
using ShopBanQuanAoOnline.Services;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>
    /// Module 3 - Bán hàng trực tiếp tại quầy: chọn sản phẩm/size/màu/số lượng → kiểm tra tồn → tính tổng
    /// → ghi nhận tiền khách đưa → lập hóa đơn nội bộ (hoàn tất ngay). Giỏ quầy lưu theo phiên đăng nhập của nhân viên.
    /// </summary>
    public class BanHangController : AdminController
    {
        public const string KeyGioQuay = "gioquay";
        private readonly ApplicationDbContext _db;
        private readonly BanHangService _banHang;

        public BanHangController(ApplicationDbContext db, BanHangService banHang)
        {
            _db = db;
            _banHang = banHang;
        }

        private List<CartItem> Gio() => HttpContext.Session.GetJson<List<CartItem>>(KeyGioQuay) ?? new List<CartItem>();
        private void LuuGio(List<CartItem> g) => HttpContext.Session.SetJson(KeyGioQuay, g);

        public async Task<IActionResult> Index(string? tukhoa, string? sdt)
        {
            var gio = Gio();
            ViewBag.tukhoa = tukhoa;
            if (!string.IsNullOrWhiteSpace(tukhoa))
            {
                var tk = tukhoa.Trim();
                var q = _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation)
                    .Where(b => b.TrangThai && b.MaMhNavigation!.TrangThai);
                q = int.TryParse(tk, out var ma)
                    ? q.Where(b => b.MaMh == ma || b.MaBt == ma)
                    : q.Where(b => b.MaMhNavigation!.Ten.Contains(tk) || b.MauSac.Contains(tk));
                ViewBag.KetQua = await q.OrderBy(b => b.MaMhNavigation!.Ten).ThenBy(b => b.KichCo).Take(40).ToListAsync();
            }
            // Gắn khách hàng (tùy chọn) để hóa đơn hiện trong lịch sử mua hàng của khách
            var maKh = HttpContext.Session.GetInt32("quay_makh");
            if (!string.IsNullOrWhiteSpace(sdt))
            {
                var kh = await _db.Khachhangs.AsNoTracking().FirstOrDefaultAsync(k => k.DienThoai == sdt.Trim());
                if (kh == null) Loi("Không tìm thấy khách hàng có số điện thoại này. Có thể bán cho khách lẻ.");
                else { HttpContext.Session.SetInt32("quay_makh", kh.MaKh); maKh = kh.MaKh; }
            }
            ViewBag.KhachHang = maKh.HasValue ? await _db.Khachhangs.AsNoTracking().FirstOrDefaultAsync(k => k.MaKh == maKh) : null;
            return View(gio);
        }

        [HttpPost]
        public async Task<IActionResult> Them(int maBt, int soLuong, string? tukhoa)
        {
            var bt = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).FirstOrDefaultAsync(b => b.MaBt == maBt);
            if (bt == null || !bt.TrangThai || bt.MaMhNavigation?.TrangThai != true)
            {
                Loi("Sản phẩm không tồn tại hoặc đã ngừng bán.");
                return RedirectToAction(nameof(Index), new { tukhoa });
            }
            var gio = Gio();
            var dong = gio.FirstOrDefault(g => g.MaBt == maBt);
            int moi = (dong?.SoLuong ?? 0) + Math.Max(1, soLuong);
            if (moi > bt.SoLuongTon)
            {
                Loi($"Không đủ hàng: {bt.MaMhNavigation.Ten} ({bt.TenBienThe}) chỉ còn {bt.SoLuongTon}.");
                return RedirectToAction(nameof(Index), new { tukhoa });
            }
            if (dong == null)
                gio.Add(new CartItem { MaBt = bt.MaBt, MaMh = bt.MaMh, Ten = bt.MaMhNavigation.Ten, KichCo = bt.KichCo, MauSac = bt.MauSac, HinhAnh = bt.MaMhNavigation.HinhAnh, DonGia = bt.MaMhNavigation.GiaBan ?? 0, SoLuong = moi });
            else dong.SoLuong = moi;
            LuuGio(gio);
            return RedirectToAction(nameof(Index), new { tukhoa });
        }

        [HttpPost]
        public async Task<IActionResult> CapNhat(int maBt, int soLuong)
        {
            var gio = Gio();
            var dong = gio.FirstOrDefault(g => g.MaBt == maBt);
            if (dong != null)
            {
                if (soLuong <= 0) gio.Remove(dong);
                else
                {
                    int ton = await _db.BienThes.Where(b => b.MaBt == maBt).Select(b => b.SoLuongTon).FirstOrDefaultAsync();
                    if (soLuong > ton) Loi($"Chỉ còn {ton} sản phẩm {dong.Ten} ({dong.KichCo} / {dong.MauSac}).");
                    dong.SoLuong = Math.Min(soLuong, ton);
                    if (dong.SoLuong <= 0) gio.Remove(dong);
                }
                LuuGio(gio);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Xoa(int maBt)
        {
            var gio = Gio();
            gio.RemoveAll(g => g.MaBt == maBt);
            LuuGio(gio);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult HuyGio()
        {
            HttpContext.Session.Remove(KeyGioQuay);
            HttpContext.Session.Remove("quay_makh");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult BoKhach()
        {
            HttpContext.Session.Remove("quay_makh");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ThanhToan(int tienKhachDua, string? ghiChu)
        {
            try
            {
                int maHd = await _banHang.ThanhToanTaiQuayAsync(Gio(), MaNvHienTai, HttpContext.Session.GetInt32("quay_makh"), tienKhachDua, ghiChu);
                HttpContext.Session.Remove(KeyGioQuay);
                HttpContext.Session.Remove("quay_makh");
                ThongBao($"Thanh toán thành công. Đã lập hóa đơn HD{maHd}.");
                return RedirectToAction("Details", "Hoadons", new { id = maHd });
            }
            catch (NghiepVuException ex)
            {
                Loi(ex.Message);
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
