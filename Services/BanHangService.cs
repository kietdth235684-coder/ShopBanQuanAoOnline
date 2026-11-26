using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Services;

/// <summary>Module 3 - Bán hàng tại quầy và đổi trả cơ bản.</summary>
public class BanHangService
{
    private readonly ApplicationDbContext _db;
    private readonly KhoService _kho;

    public BanHangService(ApplicationDbContext db, KhoService kho)
    {
        _db = db;
        _kho = kho;
    }

    /// <summary>Bán tại quầy: kiểm tra tồn, tính tiền, ghi nhận thanh toán, lập hóa đơn hoàn tất, trừ kho - trong 1 transaction.</summary>
    public async Task<int> ThanhToanTaiQuayAsync(List<CartItem> dongs, int maNv, int? maKh, int tienKhachDua, string? ghiChu)
    {
        var gop = dongs.Where(d => d.SoLuong > 0).GroupBy(d => d.MaBt)
                       .Select(g => new { MaBt = g.Key, SoLuong = g.Sum(x => x.SoLuong) }).ToList();
        if (gop.Count == 0) throw new NghiepVuException("Chưa có sản phẩm nào trong hóa đơn.");
        if (maKh.HasValue && !await _db.Khachhangs.AnyAsync(k => k.MaKh == maKh))
            throw new NghiepVuException("Khách hàng không tồn tại.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var hd = new Hoadon
        {
            Ngay = DateTime.Now,
            MaKh = maKh,
            MaNv = maNv,
            LoaiHd = HangSo.LoaiHoaDon.TaiQuay,
            TrangThai = HangSo.TrangThaiHoaDon.HoanTat,
            TienKhachDua = tienKhachDua,
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim()
        };
        foreach (var d in gop)
        {
            var bt = await _db.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).FirstOrDefaultAsync(b => b.MaBt == d.MaBt)
                     ?? throw new NghiepVuException("Sản phẩm không tồn tại.");
            if (!bt.TrangThai || bt.MaMhNavigation == null || !bt.MaMhNavigation.TrangThai)
                throw new NghiepVuException($"{bt.MaMhNavigation?.Ten} ({bt.TenBienThe}) đã ngừng bán.");
            int gia = bt.MaMhNavigation.GiaBan ?? 0;
            hd.Cthoadons.Add(new Cthoadon
            {
                MaMh = bt.MaMh,
                MaBt = bt.MaBt,
                DonGia = gia,
                SoLuong = (short)d.SoLuong,
                ThanhTien = gia * d.SoLuong,
                GiaVon = bt.MaMhNavigation.GiaNhap
            });
        }
        hd.TongTien = hd.Cthoadons.Sum(c => c.ThanhTien ?? 0);
        if (tienKhachDua < hd.TongTien)
            throw new NghiepVuException($"Tiền khách đưa ({tienKhachDua:N0}đ) nhỏ hơn tổng tiền ({hd.TongTien:N0}đ).");

        _db.Hoadons.Add(hd);
        await _db.SaveChangesAsync();
        foreach (var ct in hd.Cthoadons)
        {
            await _kho.ThayDoiTonAsync(ct.MaBt!.Value, -(ct.SoLuong ?? 0), HangSo.LoaiTheKho.BanQuay, "HD" + hd.MaHd, maNv);
            await _db.Mathangs.Where(m => m.MaMh == ct.MaMh)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LuotMua, m => (m.LuotMua ?? 0) + (ct.SoLuong ?? 0)));
        }
        await tx.CommitAsync();
        return hd.MaHd;
    }

    public record DongDoiTra(int MaCthd, int SoLuong, string HinhThuc, int? MaBtDoi, bool NhapLaiKho);

    /// <summary>
    /// Đổi trả cơ bản gắn với hóa đơn đã hoàn tất. TRA: hoàn tiền; DOI: đổi sang size/màu khác của cùng sản phẩm (không hoàn tiền).
    /// Kho được điều chỉnh theo hàng thực nhận: chỉ nhập lại kho khi hàng còn bán được (NhapLaiKho).
    /// </summary>
    public async Task<int> LapPhieuDoiTraAsync(int maHd, int maNv, string lyDo, List<DongDoiTra> dongs)
    {
        if (string.IsNullOrWhiteSpace(lyDo)) throw new NghiepVuException("Vui lòng nhập lý do đổi trả.");
        dongs = dongs.Where(d => d.SoLuong > 0).ToList();
        if (dongs.Count == 0) throw new NghiepVuException("Chưa chọn sản phẩm nào để đổi/trả.");

        var hd = await _db.Hoadons.AsNoTracking().Include(h => h.Cthoadons).FirstOrDefaultAsync(h => h.MaHd == maHd)
                 ?? throw new NghiepVuException("Không tìm thấy hóa đơn.");
        if (hd.TrangThai != HangSo.TrangThaiHoaDon.HoanTat) throw new NghiepVuException("Chỉ đổi trả cho hóa đơn đã hoàn tất.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var phieu = new PhieuDoiTra { MaHd = maHd, MaNv = maNv, NgayLap = DateTime.Now, LyDo = lyDo.Trim() };
        _db.PhieuDoiTras.Add(phieu);
        await _db.SaveChangesAsync();
        string chungTu = "DT" + phieu.MaPdt;

        foreach (var d in dongs)
        {
            var ct = hd.Cthoadons.FirstOrDefault(c => c.MaCthd == d.MaCthd)
                     ?? throw new NghiepVuException("Dòng hàng không thuộc hóa đơn này.");
            if (ct.MaBt == null) throw new NghiepVuException("Dòng hàng cũ chưa gắn biến thể, không thể đổi trả.");
            int daDoiTra = await _db.CtDoiTras.Where(x => x.MaCthd == ct.MaCthd).SumAsync(x => (int?)x.SoLuong) ?? 0;
            int conLai = (ct.SoLuong ?? 0) - daDoiTra;
            if (d.SoLuong > conLai) throw new NghiepVuException($"Số lượng đổi/trả vượt quá số đã mua (còn được đổi/trả {conLai}).");

            var dong = new CtDoiTra
            {
                MaPdt = phieu.MaPdt,
                MaCthd = ct.MaCthd,
                MaBtTra = ct.MaBt.Value,
                SoLuong = d.SoLuong,
                HinhThuc = d.HinhThuc == CtDoiTra.Doi ? CtDoiTra.Doi : CtDoiTra.Tra,
                NhapLaiKho = d.NhapLaiKho,
                DonGia = ct.DonGia ?? 0
            };
            if (dong.HinhThuc == CtDoiTra.Doi)
            {
                if (d.MaBtDoi == null) throw new NghiepVuException("Vui lòng chọn size/màu muốn đổi sang.");
                var btMoi = await _db.BienThes.AsNoTracking().FirstOrDefaultAsync(b => b.MaBt == d.MaBtDoi);
                if (btMoi == null || btMoi.MaMh != ct.MaMh) throw new NghiepVuException("Chỉ đổi sang size/màu khác của cùng sản phẩm.");
                dong.MaBtDoi = btMoi.MaBt;
            }
            else
            {
                dong.TienHoan = dong.DonGia * d.SoLuong;
            }
            _db.CtDoiTras.Add(dong);
            await _db.SaveChangesAsync();

            if (dong.NhapLaiKho)
                await _kho.ThayDoiTonAsync(dong.MaBtTra, dong.SoLuong, HangSo.LoaiTheKho.DoiTraNhap, chungTu, maNv);
            if (dong.HinhThuc == CtDoiTra.Doi)
                await _kho.ThayDoiTonAsync(dong.MaBtDoi!.Value, -dong.SoLuong, HangSo.LoaiTheKho.DoiTraXuat, chungTu, maNv);
        }

        int tong = await _db.CtDoiTras.Where(x => x.MaPdt == phieu.MaPdt).SumAsync(x => x.TienHoan);
        await _db.PhieuDoiTras.Where(p => p.MaPdt == phieu.MaPdt).ExecuteUpdateAsync(s => s.SetProperty(p => p.TienHoan, tong));
        await tx.CommitAsync();
        return phieu.MaPdt;
    }
}
