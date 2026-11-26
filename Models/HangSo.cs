namespace ShopBanQuanAoOnline.Models;

/// <summary>Các hằng số nghiệp vụ dùng chung giữa C# và SQL (giá trị phải khớp CHECK constraint trong CSDL).</summary>
public static class HangSo
{
    public static class VaiTro
    {
        public const string ChuCuaHang = "ChuCuaHang";
        public const string NhanVien = "NhanVien";
        public static string Ten(string? v) => v == ChuCuaHang ? "Chủ cửa hàng" : "Nhân viên";
    }

    /// <summary>Trạng thái xử lý đơn COD.</summary>
    public static class TrangThaiDon
    {
        public const int ChoXacNhan = 0;
        public const int DaXacNhan = 1;
        public const int DangGiao = 2;
        public const int HoanTat = 3;
        public const int DaHuy = 4;
        public const int GiaoThatBai = 5;

        public static readonly int[] TatCa = { ChoXacNhan, DaXacNhan, DangGiao, HoanTat, DaHuy, GiaoThatBai };

        public static string Ten(int t) => t switch
        {
            ChoXacNhan => "Chờ xác nhận",
            DaXacNhan => "Đã xác nhận",
            DangGiao => "Đang giao hàng",
            HoanTat => "Hoàn tất",
            DaHuy => "Đã hủy",
            GiaoThatBai => "Giao thất bại",
            _ => "Không xác định"
        };

        public static string Mau(int t) => t switch
        {
            ChoXacNhan => "warning",
            DaXacNhan => "info",
            DangGiao => "primary",
            HoanTat => "success",
            DaHuy => "secondary",
            GiaoThatBai => "danger",
            _ => "light"
        };
    }

    public static class ThanhToan
    {
        public const int ChuaThu = 0;
        public const int DaThu = 1;
        public static string Ten(int t) => t == DaThu ? "Đã thu tiền" : "Chưa thu tiền";
    }

    public static class LoaiHoaDon
    {
        public const int TaiQuay = 0;
        public const int Cod = 1;
        public static string Ten(int t) => t == Cod ? "Đơn COD" : "Tại quầy";
    }

    public static class TrangThaiHoaDon
    {
        public const int DuLieuCu = 0;   // hóa đơn tạo trước khi nâng cấp hệ thống, chưa xác nhận thu tiền
        public const int HoanTat = 1;
    }

    public static class TrangThaiDanhGia
    {
        public const int ChoDuyet = 0;
        public const int HienThi = 1;
        public const int An = 2;
        public static string Ten(int t) => t switch { HienThi => "Đang hiển thị", An => "Đã ẩn", _ => "Chờ duyệt" };
        public static string Mau(int t) => t switch { HienThi => "success", An => "secondary", _ => "warning" };
    }

    /// <summary>Loại biến động trong thẻ kho.</summary>
    public static class LoaiTheKho
    {
        public const string KhoiTao = "KHOI_TAO";
        public const string Nhap = "NHAP";
        public const string Xuat = "XUAT";
        public const string KiemKe = "KIEM_KE";
        public const string BanQuay = "BAN_QUAY";
        public const string BanCod = "BAN_COD";
        public const string HoanCod = "HOAN_COD";
        public const string DoiTraNhap = "DOITRA_NHAP";
        public const string DoiTraXuat = "DOITRA_XUAT";

        public static string Ten(string l) => l switch
        {
            KhoiTao => "Tồn đầu",
            Nhap => "Nhập hàng",
            Xuat => "Xuất kho",
            KiemKe => "Điều chỉnh kiểm kê",
            BanQuay => "Bán tại quầy",
            BanCod => "Xuất bán COD",
            HoanCod => "Hoàn kho COD",
            DoiTraNhap => "Nhận hàng đổi/trả",
            DoiTraXuat => "Xuất hàng đổi",
            _ => l
        };
    }
}
