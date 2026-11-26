using Microsoft.AspNetCore.Mvc;

namespace ShopBanQuanAoOnline.Infrastructure;

/// <summary>Kiểm tra returnUrl sau đăng nhập: chống open-redirect và không cho khách hàng bị chuyển vào khu quản trị.</summary>
public static class DangNhapHelper
{
    /// <summary>Đường dẫn thuộc website khách hàng: "/" hoặc "/Customers/...".</summary>
    public static bool LaDuongDanKhachHang(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        string path;
        try { path = Uri.UnescapeDataString(url.Split('?', '#')[0]).Trim(); }
        catch (UriFormatException) { return false; }
        if (path == "/") return true;
        return path.Equals("/Customers", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Customers/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Khách hàng: chỉ chấp nhận returnUrl cục bộ và nằm trong website khách hàng.</summary>
    public static string? ChoKhachHang(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) && LaDuongDanKhachHang(returnUrl) ? returnUrl : null;

    /// <summary>Nhân viên/chủ cửa hàng: returnUrl cục bộ, không phải trang khách hàng (tránh vòng lặp /Customers/Login).</summary>
    public static string? ChoNhanVien(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl)
        && !LaDuongDanKhachHang(returnUrl)
        && !returnUrl.StartsWith("/TaiKhoan/DangNhap", StringComparison.OrdinalIgnoreCase) ? returnUrl : null;
}
