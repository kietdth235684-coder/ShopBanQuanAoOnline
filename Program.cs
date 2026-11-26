using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;
using ShopBanQuanAoOnline.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== Kết nối SQL Server: đọc từ appsettings.json -> ConnectionStrings:DefaultConnection =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:DefaultConnection trong appsettings.json.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

// ===== MVC: tự động kiểm tra chống giả mạo (anti-forgery) cho mọi request POST =====
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// ===== Session: giỏ hàng khách, đăng nhập khách hàng, giỏ bán tại quầy =====
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(2);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// ===== Module 6: đăng nhập & phân quyền nhân viên (cookie riêng cho khu quản trị) =====
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "MikaAdmin";
        o.LoginPath = "/Customers/Login"; // trang đăng nhập DUY NHẤT cho khách hàng, nhân viên và chủ cửa hàng
        o.LogoutPath = "/TaiKhoan/DangXuat";
        o.AccessDeniedPath = "/TaiKhoan/TuChoi";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // Tài khoản bị khóa/ngừng làm việc sau khi đăng nhập sẽ bị đăng xuất ở request kế tiếp
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var id = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var role = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            bool hopLe = int.TryParse(id, out var maNv)
                         && await db.Nhanviens.AnyAsync(n => n.MaNv == maNv && n.TrangThai && n.VaiTro == role);
            if (!hopLe)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(PhanQuyen.NhanVien, p => p.RequireRole(HangSo.VaiTro.ChuCuaHang, HangSo.VaiTro.NhanVien));
    o.AddPolicy(PhanQuyen.ChuCuaHang, p => p.RequireRole(HangSo.VaiTro.ChuCuaHang));
});

// Mã hóa mật khẩu (khách hàng và nhân viên)
builder.Services.AddSingleton<IPasswordHasher<Khachhang>, PasswordHasher<Khachhang>>();
builder.Services.AddSingleton<IPasswordHasher<Nhanvien>, PasswordHasher<Nhanvien>>();

// ===== Tầng nghiệp vụ =====
builder.Services.AddScoped<KhoService>();
builder.Services.AddScoped<DonHangService>();
builder.Services.AddScoped<BanHangService>();
builder.Services.AddScoped<DanhGiaService>();
builder.Services.AddScoped<BaoCaoService>();

var app = builder.Build();

var (sqlServer, sqlDatabase) = KiemTraKetNoi.DocThongTin(connectionString);
app.Logger.LogInformation("SQL Server: {Server} | Database: {Database}  (sửa trong appsettings.json -> ConnectionStrings:DefaultConnection)", sqlServer, sqlDatabase);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Sai tên server / chưa chạy script SQL -> trang hướng dẫn tiếng Việt
app.UseHuongDanKetNoiSql(connectionString);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Trang mặc định là website khách hàng; khu quản trị bắt đầu tại /Home (yêu cầu đăng nhập nhân viên)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customers}/{action=Index}/{id?}");

app.Run();
