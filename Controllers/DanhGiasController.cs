using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>Module 5 - Nhân viên / chủ cửa hàng duyệt hoặc ẩn đánh giá sản phẩm.</summary>
    public class DanhGiasController : AdminController
    {
        private readonly ApplicationDbContext _db;
        public DanhGiasController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(int? trangThai, int? maMh, int? soSao)
        {
            var q = _db.DanhGias.AsNoTracking().Include(d => d.MaKhNavigation).Include(d => d.MaMhNavigation).AsQueryable();
            if (trangThai.HasValue) q = q.Where(d => d.TrangThai == trangThai);
            if (maMh.HasValue) q = q.Where(d => d.MaMh == maMh);
            if (soSao.HasValue) q = q.Where(d => d.SoSao == soSao);
            ViewBag.TrangThai = trangThai; ViewBag.MaMh = maMh; ViewBag.SoSao = soSao;
            ViewBag.ChoDuyet = await _db.DanhGias.CountAsync(d => d.TrangThai == HangSo.TrangThaiDanhGia.ChoDuyet);
            return View(await q.OrderBy(d => d.TrangThai == HangSo.TrangThaiDanhGia.ChoDuyet ? 0 : 1).ThenByDescending(d => d.NgayDanhGia).Take(300).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThai(int id, int trangThai, int? locTrangThai)
        {
            if (trangThai != HangSo.TrangThaiDanhGia.HienThi && trangThai != HangSo.TrangThaiDanhGia.An) return BadRequest();
            int rows = await _db.DanhGias.Where(d => d.MaDg == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.TrangThai, trangThai));
            if (rows == 0) Loi("Không tìm thấy đánh giá.");
            else ThongBao(trangThai == HangSo.TrangThaiDanhGia.HienThi ? "Đã duyệt – đánh giá hiển thị trên trang sản phẩm." : "Đã ẩn đánh giá.");
            return RedirectToAction(nameof(Index), new { trangThai = locTrangThai });
        }
    }
}
