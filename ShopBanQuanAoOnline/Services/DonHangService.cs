using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Models;
using TT = ShopBanQuanAoOnline.Models.HangSo.TrangThaiDon;

namespace ShopBanQuanAoOnline.Services;

/// <summary>
/// Module 3 + 4 - Vòng đời đơn COD:
///   Khách đặt (0 Chờ xác nhận) → NV xác nhận, trừ kho (1) → Đang giao (2)
///   → Giao thành công + đã thu tiền (3 Hoàn tất) → tạo HÓA ĐƠN nội bộ (duy nhất).
///   Nhánh: Hủy (4) khi 0 hoặc 1; Giao thất bại (5) khi 2. Hủy/thất bại sau khi đã trừ kho thì hoàn kho.
/// Mỗi bước chuyển trạng thái dùng UPDATE có điều kiện trạng thái cũ → bấm lặp/2 người cùng bấm cũng không xử lý 2 lần.
/// </summary>
public class DonHangService
{
    private readonly ApplicationDbContext _db;
    private readonly KhoService _kho;

    public DonHangService(ApplicationDbContext db, KhoService kho)
    {
        _db = db;
        _kho = kho;
    }

    /// <summary>Khách đặt hàng COD: chỉ tạo đơn chờ xác nhận, CHƯA trừ kho, CHƯA tạo hóa đơn.</summary>
    public async Task<DonHang> TaoDonAsync(int maKh, List<CartItem> gio, string hoTen, string dienThoai, string diaChi, string? ghiChu)
    {
        if (gio.Count == 0) throw new NghiepVuException("Giỏ hàng đang trống.");
        if (string.IsNullOrWhiteSpace(hoTen) || string.IsNullOrWhiteSpace(dienThoai) || string.IsNullOrWhiteSpace(diaChi))
            throw new NghiepVuException("Vui lòng nhập đầy đủ họ tên, số điện thoại và địa chỉ nhận hàng.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(dienThoai.Trim(), @"^0\d{9,10}$"))
            throw new NghiepVuException("Số điện thoại không hợp lệ (10–11 số, bắt đầu bằng 0).");

        var don = new DonHang
        {
            MaKh = maKh,
            NgayDat = DateTime.Now,
            HoTenNhan = hoTen.Trim(),
            DienThoaiNhan = dienThoai.Trim(),
            DiaChiNhan = diaChi.Trim(),
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim(),
            TrangThai = TT.ChoXacNhan,
            ThanhToan = HangSo.ThanhToan.ChuaThu
        };

        foreach (var dong in gio.GroupBy(g => g.MaBt).Select(g => new { MaBt = g.Key, SoLuong = g.Sum(x => x.SoLuong) }))
        {
            var bt = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).FirstOrDefaultAsync(b => b.MaBt == dong.MaBt);
            if (bt == null || !bt.TrangThai || bt.MaMhNavigation == null || !bt.MaMhNavigation.TrangThai)
                throw new NghiepVuException("Có sản phẩm trong giỏ đã ngừng bán, vui lòng xóa khỏi giỏ.");
            if (dong.SoLuong <= 0) throw new NghiepVuException("Số lượng không hợp lệ.");
            if (bt.SoLuongTon < dong.SoLuong)
                throw new NghiepVuException($"{bt.MaMhNavigation.Ten} ({bt.TenBienThe}) chỉ còn {bt.SoLuongTon} sản phẩm.");
            int gia = bt.MaMhNavigation.GiaBan ?? 0; // luôn lấy giá từ CSDL, không tin giá trong session
            don.CtDonHangs.Add(new CtDonHang { MaBt = bt.MaBt, DonGia = gia, SoLuong = dong.SoLuong, ThanhTien = gia * dong.SoLuong });
        }
        don.TongTien = don.CtDonHangs.Sum(c => c.ThanhTien);
        _db.DonHangs.Add(don);
        await _db.SaveChangesAsync();
        return don;
    }

    /// <summary>Khách tự hủy: chỉ khi đơn còn chờ xác nhận và là đơn của chính khách.</summary>
    public async Task KhachHuyAsync(int maDh, int maKh, string? lyDo)
    {
        int rows = await _db.DonHangs
            .Where(d => d.MaDh == maDh && d.MaKh == maKh && d.TrangThai == TT.ChoXacNhan)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.TrangThai, TT.DaHuy)
                .SetProperty(d => d.NgayCapNhat, DateTime.Now)
                .SetProperty(d => d.LyDo, "Khách hủy" + (string.IsNullOrWhiteSpace(lyDo) ? "" : ": " + lyDo.Trim())));
        if (rows == 0) throw new NghiepVuException("Chỉ hủy được đơn hàng đang chờ xác nhận.");
    }

    public async Task XacNhanAsync(int maDh, int maNv)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await ChuyenTrangThaiAsync(maDh, TT.ChoXacNhan, TT.DaXacNhan, maNv, null);
        // Kiểm tra và trừ tồn kho tại thời điểm xác nhận (tránh bán vượt số lượng)
        foreach (var ct in await LayChiTietAsync(maDh))
            await _kho.ThayDoiTonAsync(ct.MaBt, -ct.SoLuong, HangSo.LoaiTheKho.BanCod, "DH" + maDh, maNv);
        await tx.CommitAsync();
    }

    public async Task BatDauGiaoAsync(int maDh, int maNv)
    {
        await ChuyenTrangThaiAsync(maDh, TT.DaXacNhan, TT.DangGiao, maNv, null);
    }

    /// <summary>Giao thành công và đã thu tiền COD → hoàn tất đơn + tạo hóa đơn nội bộ (một lần duy nhất).</summary>
    public async Task<int> GiaoThanhCongAsync(int maDh, int maNv)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        int rows = await _db.DonHangs
            .Where(d => d.MaDh == maDh && d.TrangThai == TT.DangGiao)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.TrangThai, TT.HoanTat)
                .SetProperty(d => d.ThanhToan, HangSo.ThanhToan.DaThu)
                .SetProperty(d => d.MaNv, maNv)
                .SetProperty(d => d.NgayCapNhat, DateTime.Now));
        if (rows == 0)
        {
            var daCo = await _db.Hoadons.Where(h => h.MaDh == maDh).Select(h => (int?)h.MaHd).FirstOrDefaultAsync();
            if (daCo.HasValue)
                throw new NghiepVuException($"Đơn #{maDh} đã hoàn tất trước đó với hóa đơn #{daCo}. Không tạo hóa đơn trùng.");
            throw new NghiepVuException("Chỉ xác nhận giao thành công cho đơn đang giao.");
        }
        if (await _db.Hoadons.AnyAsync(h => h.MaDh == maDh))
            throw new NghiepVuException($"Đơn #{maDh} đã có hóa đơn. Không tạo hóa đơn trùng.");

        var don = await _db.DonHangs.AsNoTracking().FirstAsync(d => d.MaDh == maDh);
        var hd = new Hoadon
        {
            Ngay = DateTime.Now,
            MaKh = don.MaKh,
            MaDh = maDh,
            MaNv = maNv,
            LoaiHd = HangSo.LoaiHoaDon.Cod,
            TrangThai = HangSo.TrangThaiHoaDon.HoanTat,
            TongTien = don.TongTien,
            TienKhachDua = don.TongTien,
            GhiChu = "Thu tiền khi giao hàng (COD)"
        };
        foreach (var ct in await LayChiTietAsync(maDh))
        {
            var bt = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).FirstAsync(b => b.MaBt == ct.MaBt);
            hd.Cthoadons.Add(new Cthoadon
            {
                MaMh = bt.MaMh,
                MaBt = bt.MaBt,
                DonGia = ct.DonGia,
                SoLuong = (short)ct.SoLuong,
                ThanhTien = ct.ThanhTien,
                GiaVon = bt.MaMhNavigation?.GiaNhap ?? 0
            });
            await _db.Mathangs.Where(m => m.MaMh == bt.MaMh)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LuotMua, m => (m.LuotMua ?? 0) + ct.SoLuong));
        }
        _db.Hoadons.Add(hd);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return hd.MaHd;
    }

    public async Task GiaoThatBaiAsync(int maDh, int maNv, string? lyDo)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await ChuyenTrangThaiAsync(maDh, TT.DangGiao, TT.GiaoThatBai, maNv, "Giao thất bại" + NoiLyDo(lyDo));
        foreach (var ct in await LayChiTietAsync(maDh))
            await _kho.ThayDoiTonAsync(ct.MaBt, ct.SoLuong, HangSo.LoaiTheKho.HoanCod, "DH" + maDh, maNv);
        await tx.CommitAsync();
    }

    /// <summary>Nhân viên hủy đơn (khi chờ xác nhận hoặc đã xác nhận nhưng chưa giao). Đã trừ kho thì hoàn kho.</summary>
    public async Task NhanVienHuyAsync(int maDh, int maNv, string? lyDo)
    {
        var hienTai = await _db.DonHangs.Where(d => d.MaDh == maDh).Select(d => (int?)d.TrangThai).FirstOrDefaultAsync()
                      ?? throw new NghiepVuException("Không tìm thấy đơn hàng.");
        if (hienTai != TT.ChoXacNhan && hienTai != TT.DaXacNhan)
            throw new NghiepVuException("Chỉ hủy được đơn đang chờ xác nhận hoặc đã xác nhận (chưa giao).");

        await using var tx = await _db.Database.BeginTransactionAsync();
        await ChuyenTrangThaiAsync(maDh, hienTai, TT.DaHuy, maNv, "Cửa hàng hủy" + NoiLyDo(lyDo));
        if (hienTai == TT.DaXacNhan)
            foreach (var ct in await LayChiTietAsync(maDh))
                await _kho.ThayDoiTonAsync(ct.MaBt, ct.SoLuong, HangSo.LoaiTheKho.HoanCod, "DH" + maDh, maNv);
        await tx.CommitAsync();
    }

    private async Task ChuyenTrangThaiAsync(int maDh, int tu, int den, int maNv, string? lyDo)
    {
        int rows = await _db.DonHangs
            .Where(d => d.MaDh == maDh && d.TrangThai == tu)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.TrangThai, den)
                .SetProperty(d => d.MaNv, maNv)
                .SetProperty(d => d.NgayCapNhat, DateTime.Now)
                .SetProperty(d => d.LyDo, d => lyDo ?? d.LyDo));
        if (rows == 0)
            throw new NghiepVuException($"Đơn #{maDh} không còn ở trạng thái \"{TT.Ten(tu)}\" (có thể đã được xử lý). Vui lòng tải lại trang.");
    }

    private Task<List<CtDonHang>> LayChiTietAsync(int maDh) =>
        _db.CtDonHangs.AsNoTracking().Where(c => c.MaDh == maDh).ToListAsync();

    private static string NoiLyDo(string? lyDo) => string.IsNullOrWhiteSpace(lyDo) ? "" : ": " + lyDo.Trim();
}
