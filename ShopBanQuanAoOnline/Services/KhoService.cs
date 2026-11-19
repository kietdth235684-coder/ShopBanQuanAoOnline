using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Services;

/// <summary>Lỗi nghiệp vụ (không đủ tồn kho, sai trạng thái...) - thông báo hiển thị được cho người dùng.</summary>
public class NghiepVuException : Exception
{
    public NghiepVuException(string message) : base(message) { }
}

/// <summary>
/// Module 2 - Nơi DUY NHẤT thay đổi tồn kho. Mọi nghiệp vụ (nhập, xuất, kiểm kê, bán quầy, COD, đổi trả)
/// đều gọi ThayDoiTonAsync để: (1) cập nhật tồn có điều kiện, không cho âm; (2) ghi thẻ kho;
/// (3) đồng bộ MATHANG.SoLuong = tổng tồn biến thể. Phương thức phải được gọi bên trong transaction của nghiệp vụ.
/// </summary>
public class KhoService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public KhoService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Ngưỡng cảnh báo tồn kho thấp (cấu hình Kho:NguongTonThap trong appsettings.json).</summary>
    public int NguongTonThap => _config.GetValue<int?>("Kho:NguongTonThap") ?? 5;

    public async Task<int> ThayDoiTonAsync(int maBt, int delta, string loai, string? chungTu, int? maNv)
    {
        if (delta == 0) throw new NghiepVuException("Số lượng thay đổi phải khác 0.");

        // Cập nhật nguyên tử ở mức SQL: chỉ trừ khi còn đủ hàng -> không bán vượt tồn dù nhiều người thao tác cùng lúc
        int rows = delta < 0
            ? await _db.BienThes.Where(b => b.MaBt == maBt && b.SoLuongTon >= -delta)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.SoLuongTon, b => b.SoLuongTon + delta))
            : await _db.BienThes.Where(b => b.MaBt == maBt)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.SoLuongTon, b => b.SoLuongTon + delta));

        var bt = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).FirstOrDefaultAsync(b => b.MaBt == maBt)
                 ?? throw new NghiepVuException($"Không tìm thấy biến thể sản phẩm #{maBt}.");
        if (rows == 0)
            throw new NghiepVuException($"Không đủ tồn kho: {bt.MaMhNavigation?.Ten} ({bt.TenBienThe}) chỉ còn {bt.SoLuongTon}, cần {-delta}.");

        _db.TheKhos.Add(new TheKho
        {
            MaBt = maBt,
            Ngay = DateTime.Now,
            Loai = loai,
            SoLuong = delta,
            TonSau = bt.SoLuongTon,
            ChungTu = chungTu,
            MaNv = maNv
        });
        await _db.SaveChangesAsync();
        await DongBoTonSanPhamAsync(bt.MaMh);
        return bt.SoLuongTon;
    }

    public async Task DongBoTonSanPhamAsync(int maMh)
    {
        int tong = await _db.BienThes.Where(b => b.MaMh == maMh).SumAsync(b => (int?)b.SoLuongTon) ?? 0;
        short? giaTri = (short)Math.Min(tong, short.MaxValue);
        await _db.Mathangs.Where(m => m.MaMh == maMh)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.SoLuong, giaTri));
    }

    // ---------------------- Phiếu nhập ----------------------
    public record DongPhieu(int MaBt, int SoLuong, int DonGia);

    public async Task<int> LapPhieuNhapAsync(int maNcc, int? maNv, string? ghiChu, List<DongPhieu> dongs)
    {
        dongs = GopDong(dongs);
        if (dongs.Count == 0) throw new NghiepVuException("Phiếu nhập phải có ít nhất 1 dòng hàng.");
        if (dongs.Any(d => d.SoLuong <= 0 || d.DonGia < 0)) throw new NghiepVuException("Số lượng phải > 0 và đơn giá nhập không âm.");
        if (!await _db.NhaCungCaps.AnyAsync(n => n.MaNcc == maNcc && n.TrangThai))
            throw new NghiepVuException("Nhà cung cấp không hợp lệ hoặc đã ngừng hợp tác.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var phieu = new PhieuNhap { MaNcc = maNcc, MaNv = maNv, NgayNhap = DateTime.Now, GhiChu = ghiChu };
        foreach (var d in dongs)
            phieu.CtPhieuNhaps.Add(new CtPhieuNhap { MaBt = d.MaBt, SoLuong = d.SoLuong, DonGiaNhap = d.DonGia, ThanhTien = d.SoLuong * d.DonGia });
        phieu.TongTien = phieu.CtPhieuNhaps.Sum(c => c.ThanhTien);
        _db.PhieuNhaps.Add(phieu);
        await _db.SaveChangesAsync();

        foreach (var d in dongs)
        {
            var bt = await _db.BienThes.AsNoTracking().FirstOrDefaultAsync(b => b.MaBt == d.MaBt)
                     ?? throw new NghiepVuException($"Biến thể #{d.MaBt} không tồn tại.");
            // Cập nhật giá nhập tham khảo theo bình quân gia quyền (phục vụ lợi nhuận ước tính)
            var sp = await _db.Mathangs.AsNoTracking().FirstAsync(m => m.MaMh == bt.MaMh);
            long tonCu = await _db.BienThes.Where(b => b.MaMh == bt.MaMh).SumAsync(b => (long)b.SoLuongTon);
            long giaMoi = tonCu + d.SoLuong == 0 ? d.DonGia : (tonCu * sp.GiaNhap + (long)d.SoLuong * d.DonGia) / (tonCu + d.SoLuong);
            await _db.Mathangs.Where(m => m.MaMh == bt.MaMh).ExecuteUpdateAsync(s => s.SetProperty(m => m.GiaNhap, (int)giaMoi));

            await ThayDoiTonAsync(d.MaBt, d.SoLuong, HangSo.LoaiTheKho.Nhap, "PN" + phieu.MaPn, maNv);
        }
        await tx.CommitAsync();
        return phieu.MaPn;
    }

    // ---------------------- Phiếu xuất ----------------------
    public async Task<int> LapPhieuXuatAsync(int? maNv, string lyDo, string? ghiChu, List<DongPhieu> dongs)
    {
        dongs = GopDong(dongs);
        if (string.IsNullOrWhiteSpace(lyDo)) throw new NghiepVuException("Vui lòng nhập lý do xuất kho.");
        if (dongs.Count == 0) throw new NghiepVuException("Phiếu xuất phải có ít nhất 1 dòng hàng.");
        if (dongs.Any(d => d.SoLuong <= 0)) throw new NghiepVuException("Số lượng xuất phải > 0.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var phieu = new PhieuXuat { MaNv = maNv, NgayXuat = DateTime.Now, LyDo = lyDo.Trim(), GhiChu = ghiChu };
        foreach (var d in dongs) phieu.CtPhieuXuats.Add(new CtPhieuXuat { MaBt = d.MaBt, SoLuong = d.SoLuong });
        _db.PhieuXuats.Add(phieu);
        await _db.SaveChangesAsync();
        foreach (var d in dongs)
            await ThayDoiTonAsync(d.MaBt, -d.SoLuong, HangSo.LoaiTheKho.Xuat, "PX" + phieu.MaPx, maNv);
        await tx.CommitAsync();
        return phieu.MaPx;
    }

    // ---------------------- Kiểm kê ----------------------
    public async Task<int> LapPhieuKiemKeAsync(int? maNv, string? ghiChu, Dictionary<int, int> thucTe)
    {
        if (thucTe.Count == 0) throw new NghiepVuException("Chưa có dòng kiểm kê nào.");
        if (thucTe.Values.Any(v => v < 0)) throw new NghiepVuException("Số lượng thực tế không được âm.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var phieu = new PhieuKiemKe { MaNv = maNv, NgayKiemKe = DateTime.Now, GhiChu = ghiChu };
        _db.PhieuKiemKes.Add(phieu);
        await _db.SaveChangesAsync();

        foreach (var (maBt, sl) in thucTe)
        {
            // Đọc tồn hệ thống tại thời điểm lưu phiếu (không dùng số liệu cũ trên form)
            int heThong = await _db.BienThes.Where(b => b.MaBt == maBt).Select(b => (int?)b.SoLuongTon).FirstOrDefaultAsync()
                          ?? throw new NghiepVuException($"Biến thể #{maBt} không tồn tại.");
            int chenh = sl - heThong;
            _db.CtKiemKes.Add(new CtKiemKe { MaKk = phieu.MaKk, MaBt = maBt, SoLuongHeThong = heThong, SoLuongThucTe = sl, ChenhLech = chenh });
            await _db.SaveChangesAsync();
            if (chenh != 0)
                await ThayDoiTonAsync(maBt, chenh, HangSo.LoaiTheKho.KiemKe, "KK" + phieu.MaKk, maNv);
        }
        await tx.CommitAsync();
        return phieu.MaKk;
    }

    private static List<DongPhieu> GopDong(List<DongPhieu> dongs) =>
        dongs.Where(d => d.MaBt > 0)
             .GroupBy(d => new { d.MaBt, d.DonGia })
             .Select(g => new DongPhieu(g.Key.MaBt, g.Sum(x => x.SoLuong), g.Key.DonGia))
             .ToList();
}
