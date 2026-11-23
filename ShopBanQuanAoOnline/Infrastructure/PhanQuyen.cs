using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Infrastructure;

public static class PhanQuyen
{
    /// <summary>Chủ cửa hàng hoặc nhân viên.</summary>
    public const string NhanVien = "NhanVien";
    /// <summary>Chỉ chủ cửa hàng.</summary>
    public const string ChuCuaHang = "ChuCuaHang";
}

/// <summary>
/// Lớp cơ sở cho mọi controller khu quản trị: bắt buộc đăng nhập nhân viên.
/// Hành động chỉ dành cho chủ cửa hàng đánh dấu thêm [Authorize(Policy = PhanQuyen.ChuCuaHang)].
/// </summary>
[Authorize(Policy = PhanQuyen.NhanVien)]
public abstract class AdminController : Controller
{
    protected int MaNvHienTai => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected bool LaChuCuaHang => User.IsInRole(HangSo.VaiTro.ChuCuaHang);

    protected void ThongBao(string noiDung) => TempData["ThongBao"] = noiDung;
    protected void Loi(string noiDung) => TempData["Loi"] = noiDung;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ViewData["LaChuCuaHang"] = LaChuCuaHang;
        base.OnActionExecuting(context);
    }
}
