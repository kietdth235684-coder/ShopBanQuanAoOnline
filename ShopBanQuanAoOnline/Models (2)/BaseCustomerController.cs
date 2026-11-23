using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>
    /// Lớp cơ sở cho các trang website khách hàng: cung cấp dữ liệu layout (danh mục, số lượng giỏ, khách đang đăng nhập),
    /// đọc/ghi giỏ hàng trong Session. Khách hàng đăng nhập bằng Session (khóa "khachhang" = email, "makh" = mã KH).
    /// </summary>
    public abstract class BaseCustomerController : Controller
    {
        protected const string KeyGio = "shopcart";
        protected readonly ApplicationDbContext _context;

        protected BaseCustomerController(ApplicationDbContext context)
        {
            _context = context;
        }

        protected int? MaKhDangNhap => HttpContext.Session.GetInt32("makh");

        protected List<CartItem> GetCartItems() => HttpContext.Session.GetJson<List<CartItem>>(KeyGio) ?? new List<CartItem>();
        protected void SaveCartSession(List<CartItem> list) => HttpContext.Session.SetJson(KeyGio, list);
        protected void ClearCart() => HttpContext.Session.Remove(KeyGio);

        protected void ThongBao(string s) => TempData["ThongBao"] = s;
        protected void Loi(string s) => TempData["Loi"] = s;

        /// <summary>Dữ liệu dùng chung cho _LayoutCustomer (tự động gọi trước mỗi action).</summary>
        protected void GetData()
        {
            ViewData["solg"] = GetCartItems().Sum(i => i.SoLuong);
            ViewBag.danhmuc = _context.Danhmucs.AsNoTracking().OrderBy(d => d.Ten).ToList();
            ViewBag.cuahang = _context.Cuahangs.AsNoTracking().FirstOrDefault();
            var ma = MaKhDangNhap;
            if (ma.HasValue)
                ViewBag.khachhang = _context.Khachhangs.AsNoTracking().FirstOrDefault(k => k.MaKh == ma);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            GetData();
            base.OnActionExecuting(context);
        }
    }
}
