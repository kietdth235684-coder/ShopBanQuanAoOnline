using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Services;

/// <summary>Module 5 - Đánh giá: chỉ khách đã mua (có hóa đơn hoàn tất chứa sản phẩm) mới được đánh giá, mỗi sản phẩm 1 lần.</summary>
public class DanhGiaService
{
    private readonly ApplicationDbContext _db;
    public DanhGiaService(ApplicationDbContext db) => _db = db;

    public Task<bool> DaMuaAsync(int maKh, int maMh) =>
        _db.Cthoadons.AnyAsync(c => c.MaMh == maMh
                                 && c.MaHdNavigation.MaKh == maKh
                                 && c.MaHdNavigation.TrangThai == HangSo.TrangThaiHoaDon.HoanTat);

    public Task<bool> DaDanhGiaAsync(int maKh, int maMh) =>
        _db.DanhGias.AnyAsync(d => d.MaKh == maKh && d.MaMh == maMh);

    public async Task GuiDanhGiaAsync(int maKh, int maMh, int soSao, string? nhanXet)
    {
        if (soSao < 1 || soSao > 5) throw new NghiepVuException("Số sao phải từ 1 đến 5.");
        if (nhanXet != null && nhanXet.Length > 1000) throw new NghiepVuException("Nhận xét tối đa 1000 ký tự.");
        if (!await DaMuaAsync(maKh, maMh)) throw new NghiepVuException("Bạn chỉ có thể đánh giá sản phẩm đã mua và đã nhận hàng.");
        if (await DaDanhGiaAsync(maKh, maMh)) throw new NghiepVuException("Bạn đã đánh giá sản phẩm này rồi.");

        _db.DanhGias.Add(new DanhGia
        {
            MaKh = maKh,
            MaMh = maMh,
            SoSao = soSao,
            NhanXet = string.IsNullOrWhiteSpace(nhanXet) ? null : nhanXet.Trim(),
            NgayDanhGia = DateTime.Now,
            TrangThai = HangSo.TrangThaiDanhGia.ChoDuyet
        });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new NghiepVuException("Bạn đã đánh giá sản phẩm này rồi."); }
    }
}
