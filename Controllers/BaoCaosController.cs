using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Services;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Module 7 - Báo cáo thống kê dành cho chủ cửa hàng.</summary>
    [Authorize(Policy = PhanQuyen.ChuCuaHang)]
    public class BaoCaosController : AdminController
    {
        private readonly BaoCaoService _bc;
        public BaoCaosController(BaoCaoService bc) => _bc = bc;

        private static (DateTime, DateTime) Ky(DateTime? tuNgay, DateTime? denNgay)
        {
            var den = denNgay ?? DateTime.Today;
            var tu = tuNgay ?? new DateTime(den.Year, den.Month, 1);
            return tu > den ? (den, tu) : (tu, den);
        }

        /// <summary>Doanh thu, lợi nhuận ước tính theo ngày / tháng / năm.</summary>
        public async Task<IActionResult> DoanhThu(DateTime? tuNgay, DateTime? denNgay, string kieu = "ngay")
        {
            if (kieu == "nam" && tuNgay == null) tuNgay = new DateTime(DateTime.Today.Year - 2, 1, 1);
            if (kieu == "thang" && tuNgay == null) tuNgay = new DateTime(DateTime.Today.Year, 1, 1);
            var (tu, den) = Ky(tuNgay, denNgay);
            return View(await _bc.DoanhThuAsync(tu, den, kieu));
        }

        public async Task<IActionResult> BanChay(DateTime? tuNgay, DateTime? denNgay, int top = 10)
        {
            var (tu, den) = Ky(tuNgay, denNgay);
            ViewBag.TuNgay = tu.ToString("yyyy-MM-dd"); ViewBag.DenNgay = den.ToString("yyyy-MM-dd"); ViewBag.Top = top;
            return View(await _bc.BanChayAsync(tu, den, Math.Clamp(top, 1, 100)));
        }
    }
}
