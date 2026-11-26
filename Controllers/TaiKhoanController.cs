using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers;

/// <summary>Module 6 - Đăng nhập / đăng xuất / đổi mật khẩu của nhân viên và chủ cửa hàng.</summary>
public class TaiKhoanController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<Nhanvien> _hasher;

    public TaiKhoanController(ApplicationDbContext db, IPasswordHasher<Nhanvien> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>So khớp mật khẩu an toàn: chuỗi băm không đúng định dạng (dữ liệu cũ) coi như sai mật khẩu.</summary>
    internal static bool KiemTraMatKhau<T>(IPasswordHasher<T> hasher, T user, string? hash, string matKhau) where T : class
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(matKhau)) return false;
        try { return hasher.VerifyHashedPassword(user, hash, matKhau) != PasswordVerificationResult.Failed; }
        catch (FormatException) { return false; }
    }

    /// <summary>Đường dẫn cũ: chuyển về trang đăng nhập duy nhất /Customers/Login (giữ returnUrl).</summary>
    [AllowAnonymous]
    public IActionResult DangNhap(string? returnUrl) =>
        RedirectToAction("Login", "Customers", new { returnUrl });

    [HttpPost]
    public async Task<IActionResult> DangXuat()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Remove(BanHangController.KeyGioQuay);
        HttpContext.Session.Remove("khachhang");
        HttpContext.Session.Remove("makh");
        return RedirectToAction("Login", "Customers");
    }

    [AllowAnonymous]
    public IActionResult TuChoi() => View();

    [Authorize(Policy = PhanQuyen.NhanVien)]
    public IActionResult DoiMatKhau() => View();

    [HttpPost, Authorize(Policy = PhanQuyen.NhanVien)]
    public async Task<IActionResult> DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhan)
    {
        int maNv = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var nv = await _db.Nhanviens.FindAsync(maNv);
        if (nv == null) return NotFound();
        if (!KiemTraMatKhau(_hasher, nv, nv.MatKhau, matKhauCu))
            ModelState.AddModelError("", "Mật khẩu hiện tại không đúng.");
        if (string.IsNullOrEmpty(matKhauMoi) || matKhauMoi.Length < 6)
            ModelState.AddModelError("", "Mật khẩu mới tối thiểu 6 ký tự.");
        if (matKhauMoi != xacNhan)
            ModelState.AddModelError("", "Xác nhận mật khẩu không khớp.");
        if (!ModelState.IsValid) return View();

        nv.MatKhau = _hasher.HashPassword(nv, matKhauMoi);
        await _db.SaveChangesAsync();
        TempData["ThongBao"] = "Đã đổi mật khẩu.";
        return RedirectToAction("Index", "Home");
    }
}
