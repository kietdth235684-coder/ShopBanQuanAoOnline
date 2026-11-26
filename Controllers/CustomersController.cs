using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBanQuanAoOnline.Data;
using ShopBanQuanAoOnline.Infrastructure;
using ShopBanQuanAoOnline.Models;
using ShopBanQuanAoOnline.Services;

namespace ShopBanQuanAoOnline.Controllers
{
    /// <summary>
    /// Module 4 - Website khách hàng: xem/tìm/lọc sản phẩm, giỏ hàng, đặt hàng COD, hủy đơn khi chờ xác nhận,
    /// theo dõi trạng thái, lịch sử mua hàng, xem hóa đơn sau khi hoàn tất, hồ sơ cá nhân, đánh giá sản phẩm đã mua.
    /// Website KHÔNG xác nhận giao hàng, thu tiền hay tạo hóa đơn - các việc đó thuộc khu quản trị (module 3).
    /// </summary>
    public class CustomersController : BaseCustomerController
    {
        private const int SoSpMoiTrang = 12;
        private readonly IPasswordHasher<Khachhang> _passwordHasher;
        private readonly IPasswordHasher<Nhanvien> _nvHasher;
        private readonly DonHangService _donHang;
        private readonly DanhGiaService _danhGia;

        public CustomersController(ApplicationDbContext context, IPasswordHasher<Khachhang> passwordHasher,
            IPasswordHasher<Nhanvien> nvHasher, DonHangService donHang, DanhGiaService danhGia) : base(context)
        {
            _passwordHasher = passwordHasher;
            _nvHasher = nvHasher;
            _donHang = donHang;
            _danhGia = danhGia;
        }

        private IActionResult? YeuCauDangNhap(string? returnUrl = null)
        {
            if (MaKhDangNhap.HasValue) return null;
            Loi("Vui lòng đăng nhập để tiếp tục.");
            return RedirectToAction(nameof(Login), new { returnUrl = returnUrl ?? (Request.Path + Request.QueryString) });
        }

        private IQueryable<Mathang> SanPhamDangBan() =>
            _context.Mathangs.AsNoTracking().Include(m => m.MaDmNavigation).Where(m => m.TrangThai);

        private async Task NapDiemDanhGiaAsync(IEnumerable<int> ids)
        {
            var ds = ids.ToList();
            ViewBag.DiemDanhGia = await _context.DanhGias
                .Where(d => ds.Contains(d.MaMh) && d.TrangThai == HangSo.TrangThaiDanhGia.HienThi)
                .GroupBy(d => d.MaMh).Select(g => new { g.Key, Tb = g.Average(x => (double)x.SoSao), Sl = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => (x.Tb, x.Sl));
        }

        // ======================= XEM SẢN PHẨM =======================
        public async Task<IActionResult> Index()
        {
            var moi = await SanPhamDangBan().OrderByDescending(m => m.MaMh).Take(8).ToListAsync();
            var banChay = await SanPhamDangBan().Where(m => m.LuotMua > 0).OrderByDescending(m => m.LuotMua).Take(4).ToListAsync();
            ViewBag.BanChay = banChay;
            await NapDiemDanhGiaAsync(moi.Select(m => m.MaMh).Concat(banChay.Select(m => m.MaMh)));
            return View(moi);
        }

        /// <summary>Danh sách sản phẩm có tìm kiếm, lọc theo danh mục / size / màu / giá, sắp xếp và phân trang.</summary>
        public async Task<IActionResult> SanPham(int? maDm, string? tukhoa, string? size, string? mau, int? giaTu, int? giaDen, string? sapXep, int trang = 1)
        {
            var q = SanPhamDangBan();
            if (maDm.HasValue) q = q.Where(m => m.MaDm == maDm);
            if (!string.IsNullOrWhiteSpace(tukhoa))
            {
                var tk = tukhoa.Trim();
                q = q.Where(m => m.Ten.Contains(tk) || (m.MoTa != null && m.MoTa.Contains(tk)) || m.MaDmNavigation!.Ten.Contains(tk));
            }
            if (!string.IsNullOrWhiteSpace(size)) q = q.Where(m => m.BienThes.Any(b => b.TrangThai && b.KichCo == size));
            if (!string.IsNullOrWhiteSpace(mau)) q = q.Where(m => m.BienThes.Any(b => b.TrangThai && b.MauSac == mau));
            if (giaTu.HasValue) q = q.Where(m => m.GiaBan >= giaTu);
            if (giaDen.HasValue) q = q.Where(m => m.GiaBan <= giaDen);
            q = sapXep switch
            {
                "gia-tang" => q.OrderBy(m => m.GiaBan),
                "gia-giam" => q.OrderByDescending(m => m.GiaBan),
                "ban-chay" => q.OrderByDescending(m => m.LuotMua),
                _ => q.OrderByDescending(m => m.MaMh)
            };

            int tong = await q.CountAsync();
            int soTrang = Math.Max(1, (int)Math.Ceiling(tong / (double)SoSpMoiTrang));
            trang = Math.Clamp(trang, 1, soTrang);
            var ds = await q.Skip((trang - 1) * SoSpMoiTrang).Take(SoSpMoiTrang).ToListAsync();
            await NapDiemDanhGiaAsync(ds.Select(m => m.MaMh));

            var bienThe = _context.BienThes.AsNoTracking().Where(b => b.TrangThai && b.MaMhNavigation!.TrangThai);
            ViewBag.DsSize = await bienThe.Select(b => b.KichCo).Distinct().OrderBy(x => x).ToListAsync();
            ViewBag.DsMau = await bienThe.Select(b => b.MauSac).Distinct().OrderBy(x => x).ToListAsync();
            ViewBag.tendm = maDm.HasValue ? (await _context.Danhmucs.FindAsync(maDm))?.Ten : null;
            ViewBag.Loc = new LocSanPham { MaDm = maDm, TuKhoa = tukhoa, Size = size, Mau = mau, GiaTu = giaTu, GiaDen = giaDen, SapXep = sapXep };
            ViewBag.tukhoa = tukhoa; ViewBag.Trang = trang; ViewBag.SoTrang = soTrang; ViewBag.Tong = tong;
            return View(ds);
        }

        // Giữ các đường dẫn cũ để không hỏng liên kết
        public IActionResult List(int id) => RedirectToAction(nameof(SanPham), new { maDm = id });
        public IActionResult Tim(string? tukhoa) => RedirectToAction(nameof(SanPham), new { tukhoa });

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var mathang = await SanPhamDangBan().Include(m => m.BienThes).FirstOrDefaultAsync(m => m.MaMh == id);
            if (mathang == null) return NotFound();

            // Tăng lượt xem bằng UPDATE trực tiếp (không ghi đè các cột khác)
            await _context.Mathangs.Where(m => m.MaMh == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.LuotXem, m => (m.LuotXem ?? 0) + 1));

            ViewBag.DanhGias = await _context.DanhGias.AsNoTracking().Include(d => d.MaKhNavigation)
                .Where(d => d.MaMh == id && d.TrangThai == HangSo.TrangThaiDanhGia.HienThi)
                .OrderByDescending(d => d.NgayDanhGia).ToListAsync();

            // Quyền đánh giá: đã đăng nhập + đã mua (hóa đơn hoàn tất) + chưa đánh giá
            string trangThaiDanhGia = "chua-dang-nhap";
            if (MaKhDangNhap is int maKh)
            {
                if (await _danhGia.DaDanhGiaAsync(maKh, mathang.MaMh)) trangThaiDanhGia = "da-danh-gia";
                else if (await _danhGia.DaMuaAsync(maKh, mathang.MaMh)) trangThaiDanhGia = "duoc-danh-gia";
                else trangThaiDanhGia = "chua-mua";
            }
            ViewBag.TrangThaiDanhGia = trangThaiDanhGia;
            ViewBag.LienQuan = await SanPhamDangBan().Where(m => m.MaDm == mathang.MaDm && m.MaMh != mathang.MaMh).OrderByDescending(m => m.LuotMua).Take(4).ToListAsync();
            await NapDiemDanhGiaAsync(((List<Mathang>)ViewBag.LienQuan).Select(m => m.MaMh));
            return View(mathang);
        }

        [HttpPost]
        public async Task<IActionResult> DanhGia(int maMh, int soSao, string? nhanXet)
        {
            if (YeuCauDangNhap(Url.Action(nameof(Details), new { id = maMh })) is IActionResult r) return r;
            try
            {
                await _danhGia.GuiDanhGiaAsync(MaKhDangNhap!.Value, maMh, soSao, nhanXet);
                ThongBao("Cảm ơn bạn! Đánh giá đã được gửi và sẽ hiển thị sau khi cửa hàng duyệt.");
            }
            catch (NghiepVuException ex) { Loi(ex.Message); }
            return RedirectToAction(nameof(Details), new { id = maMh });
        }

        // ======================= GIỎ HÀNG =======================
        [HttpPost]
        public async Task<IActionResult> AddToCart(int maBt, int soLuong = 1)
        {
            var bt = await _context.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).FirstOrDefaultAsync(b => b.MaBt == maBt);
            if (bt == null || !bt.TrangThai || bt.MaMhNavigation?.TrangThai != true)
            {
                Loi("Vui lòng chọn size và màu còn bán.");
                return RedirectToAction(nameof(SanPham));
            }
            var cart = GetCartItems();
            var item = cart.Find(p => p.MaBt == maBt);
            int moi = (item?.SoLuong ?? 0) + Math.Max(1, soLuong);
            if (moi > bt.SoLuongTon)
            {
                Loi(bt.SoLuongTon == 0 ? $"Size {bt.KichCo} / màu {bt.MauSac} đã hết hàng." : $"Chỉ còn {bt.SoLuongTon} sản phẩm size {bt.KichCo} / màu {bt.MauSac}.");
                return RedirectToAction(nameof(Details), new { id = bt.MaMh });
            }
            if (item == null)
                cart.Add(new CartItem { MaBt = bt.MaBt, MaMh = bt.MaMh, Ten = bt.MaMhNavigation.Ten, KichCo = bt.KichCo, MauSac = bt.MauSac, HinhAnh = bt.MaMhNavigation.HinhAnh, DonGia = bt.MaMhNavigation.GiaBan ?? 0, SoLuong = moi });
            else item.SoLuong = moi;
            SaveCartSession(cart);
            ThongBao($"Đã thêm {bt.MaMhNavigation.Ten} ({bt.TenBienThe}) vào giỏ hàng.");
            return RedirectToAction(nameof(ViewCart));
        }

        public async Task<IActionResult> ViewCart()
        {
            // Đồng bộ lại giá / tồn mới nhất để khách thấy đúng trước khi đặt
            var cart = GetCartItems();
            var ids = cart.Select(c => c.MaBt).ToList();
            var bts = await _context.BienThes.AsNoTracking().Include(b => b.MaMhNavigation).Where(b => ids.Contains(b.MaBt)).ToDictionaryAsync(b => b.MaBt);
            var canhBao = new List<string>();
            foreach (var c in cart.ToList())
            {
                if (!bts.TryGetValue(c.MaBt, out var bt) || !bt.TrangThai || bt.MaMhNavigation?.TrangThai != true)
                {
                    cart.Remove(c);
                    canhBao.Add($"{c.Ten} ({c.KichCo} / {c.MauSac}) đã ngừng bán và được xóa khỏi giỏ.");
                    continue;
                }
                c.DonGia = bt.MaMhNavigation.GiaBan ?? 0;
                if (c.SoLuong > bt.SoLuongTon)
                {
                    canhBao.Add(bt.SoLuongTon == 0 ? $"{c.Ten} ({c.KichCo} / {c.MauSac}) đã hết hàng." : $"{c.Ten} ({c.KichCo} / {c.MauSac}) chỉ còn {bt.SoLuongTon}, số lượng đã được điều chỉnh.");
                    c.SoLuong = bt.SoLuongTon;
                    if (c.SoLuong == 0) cart.Remove(c);
                }
            }
            SaveCartSession(cart);
            ViewData["solg"] = cart.Sum(i => i.SoLuong);
            ViewBag.CanhBao = canhBao;
            return View(cart);
        }

        [HttpPost]
        public IActionResult RemoveItem(int id)
        {
            var cart = GetCartItems();
            cart.RemoveAll(p => p.MaBt == id);
            SaveCartSession(cart);
            return RedirectToAction(nameof(ViewCart));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateItem(int id, int quantity)
        {
            var cart = GetCartItems();
            var item = cart.Find(p => p.MaBt == id);
            if (item != null)
            {
                if (quantity <= 0) cart.Remove(item);
                else
                {
                    int ton = await _context.BienThes.Where(b => b.MaBt == id).Select(b => b.SoLuongTon).FirstOrDefaultAsync();
                    if (quantity > ton) Loi($"Chỉ còn {ton} sản phẩm {item.Ten} ({item.KichCo} / {item.MauSac}).");
                    item.SoLuong = Math.Min(quantity, ton);
                    if (item.SoLuong <= 0) cart.Remove(item);
                }
            }
            SaveCartSession(cart);
            return RedirectToAction(nameof(ViewCart));
        }

        // ======================= ĐẶT HÀNG COD =======================
        public async Task<IActionResult> CheckOut()
        {
            if (YeuCauDangNhap(Url.Action(nameof(CheckOut))) is IActionResult r) return r;
            var cart = GetCartItems();
            if (cart.Count == 0) return RedirectToAction(nameof(ViewCart));
            ViewBag.DiaChi = await _context.Diachis.AsNoTracking().Where(d => d.MaKh == MaKhDangNhap).OrderByDescending(d => d.MacDinh).FirstOrDefaultAsync();
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> DatHang(string hoten, string dienthoai, string diachi, string? ghichu)
        {
            if (YeuCauDangNhap(Url.Action(nameof(CheckOut))) is IActionResult r) return r;
            try
            {
                var don = await _donHang.TaoDonAsync(MaKhDangNhap!.Value, GetCartItems(), hoten, dienthoai, diachi, ghichu);
                ClearCart();
                return RedirectToAction(nameof(DatHangThanhCong), new { id = don.MaDh });
            }
            catch (NghiepVuException ex)
            {
                Loi(ex.Message);
                return RedirectToAction(nameof(CheckOut));
            }
        }

        public async Task<IActionResult> DatHangThanhCong(int id)
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var don = await _context.DonHangs.AsNoTracking().FirstOrDefaultAsync(d => d.MaDh == id && d.MaKh == MaKhDangNhap);
            return don == null ? NotFound() : View(don);
        }

        // ======================= ĐƠN HÀNG CỦA TÔI =======================
        public async Task<IActionResult> DonHang(int? trangThai)
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var q = _context.DonHangs.AsNoTracking().Include(d => d.CtDonHangs).Where(d => d.MaKh == MaKhDangNhap);
            if (trangThai.HasValue) q = q.Where(d => d.TrangThai == trangThai);
            ViewBag.TrangThai = trangThai;
            return View(await q.OrderByDescending(d => d.NgayDat).ToListAsync());
        }

        public async Task<IActionResult> ChiTietDonHang(int id)
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var don = await _context.DonHangs.AsNoTracking()
                .Include(d => d.CtDonHangs).ThenInclude(c => c.MaBtNavigation).ThenInclude(b => b!.MaMhNavigation)
                .FirstOrDefaultAsync(d => d.MaDh == id && d.MaKh == MaKhDangNhap); // chỉ xem được đơn của chính mình
            if (don == null) return NotFound();
            ViewBag.MaHd = await _context.Hoadons.Where(h => h.MaDh == id).Select(h => (int?)h.MaHd).FirstOrDefaultAsync();
            return View(don);
        }

        [HttpPost]
        public async Task<IActionResult> HuyDonHang(int id, string? lyDo)
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            try
            {
                await _donHang.KhachHuyAsync(id, MaKhDangNhap!.Value, lyDo);
                ThongBao($"Đã hủy đơn hàng DH{id}.");
            }
            catch (NghiepVuException ex) { Loi(ex.Message); }
            return RedirectToAction(nameof(ChiTietDonHang), new { id });
        }

        /// <summary>Lịch sử mua hàng = các hóa đơn đã hoàn tất (mua online COD hoặc mua tại quầy có gắn tài khoản).</summary>
        public async Task<IActionResult> LichSuMua()
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var ds = await _context.Hoadons.AsNoTracking()
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaMhNavigation)
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaBtNavigation)
                .Where(h => h.MaKh == MaKhDangNhap && h.TrangThai == HangSo.TrangThaiHoaDon.HoanTat)
                .OrderByDescending(h => h.Ngay).ToListAsync();
            var maKh = MaKhDangNhap!.Value;
            ViewBag.DaDanhGia = await _context.DanhGias.Where(d => d.MaKh == maKh).Select(d => d.MaMh).ToListAsync();
            return View(ds);
        }

        /// <summary>Khách xem hóa đơn của mình (chỉ hóa đơn đã hoàn tất).</summary>
        public async Task<IActionResult> HoaDon(int id)
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var hd = await _context.Hoadons.AsNoTracking()
                .Include(h => h.MaKhNavigation).Include(h => h.MaNvNavigation).Include(h => h.MaDhNavigation)
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaMhNavigation)
                .Include(h => h.Cthoadons).ThenInclude(c => c.MaBtNavigation)
                .FirstOrDefaultAsync(h => h.MaHd == id && h.MaKh == MaKhDangNhap && h.TrangThai == HangSo.TrangThaiHoaDon.HoanTat);
            return hd == null ? NotFound() : View(hd);
        }

        // ======================= TÀI KHOẢN KHÁCH HÀNG =======================
        public IActionResult Register() => MaKhDangNhap.HasValue ? RedirectToAction(nameof(CustomerInfo)) : View();

        [HttpPost]
        public async Task<IActionResult> Register(string hoten, string dienthoai, string email, string matkhau, string xacnhan)
        {
            hoten = (hoten ?? "").Trim();
            email = (email ?? "").Trim().ToLowerInvariant();
            dienthoai = (dienthoai ?? "").Trim();
            if (hoten.Length == 0 || hoten.Length > 100) ModelState.AddModelError("", "Vui lòng nhập họ tên (tối đa 100 ký tự).");
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 100) ModelState.AddModelError("", "Email không hợp lệ.");
            if (!Regex.IsMatch(dienthoai, @"^0\d{9,10}$")) ModelState.AddModelError("", "Số điện thoại không hợp lệ (10–11 số, bắt đầu bằng 0).");
            if (string.IsNullOrEmpty(matkhau) || matkhau.Length < 6) ModelState.AddModelError("", "Mật khẩu tối thiểu 6 ký tự.");
            if (matkhau != xacnhan) ModelState.AddModelError("", "Xác nhận mật khẩu không khớp.");
            if (ModelState.IsValid && await _context.Khachhangs.AnyAsync(k => k.Email == email))
                ModelState.AddModelError("", "Email đã được đăng ký. Vui lòng đăng nhập.");
            if (!ModelState.IsValid)
            {
                ViewBag.hoten = hoten; ViewBag.email = email; ViewBag.dienthoai = dienthoai;
                return View();
            }

            var kh = new Khachhang { Ten = hoten, DienThoai = dienthoai, Email = email };
            kh.MatKhau = _passwordHasher.HashPassword(kh, matkhau); // mã hóa mật khẩu
            _context.Add(kh);
            await _context.SaveChangesAsync();
            ThongBao("Đăng ký thành công! Mời bạn đăng nhập.");
            return RedirectToAction(nameof(Login));
        }

        // ======================= ĐĂNG NHẬP DUY NHẤT (nhân viên / chủ cửa hàng / khách hàng) =======================
        // Chuỗi băm giả để tốn thời gian xác minh như thật khi email không tồn tại (chống dò email qua thời gian phản hồi).
        private static readonly string HashGia = new PasswordHasher<Khachhang>().HashPassword(new Khachhang(), "mika-hash-gia");

        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            // Đã đăng nhập khu quản trị -> vào thẳng trang quản trị
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            if (MaKhDangNhap.HasValue)
            {
                // Khách hàng bị đẩy về đây vì cố vào khu quản trị
                if (!string.IsNullOrEmpty(returnUrl) && !DangNhapHelper.LaDuongDanKhachHang(returnUrl))
                    Loi("Tài khoản khách hàng không có quyền truy cập khu quản trị.");
                return RedirectToAction(nameof(CustomerInfo));
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string matkhau, string? returnUrl)
        {
            var emailNhap = (email ?? "").Trim();
            matkhau ??= "";

            // 1) Ưu tiên bảng NHANVIEN (chủ cửa hàng + nhân viên)
            var nv = emailNhap.Length == 0 ? null
                : await _context.Nhanviens.AsNoTracking().FirstOrDefaultAsync(n => n.Email == emailNhap);
            if (nv != null && TaiKhoanController.KiemTraMatKhau(_nvHasher, nv, nv.MatKhau, matkhau))
            {
                if (!nv.TrangThai)
                {
                    // Chỉ báo "khóa" khi ĐÃ đúng mật khẩu, nên không lộ email cho người lạ
                    return ViewLoi("Tài khoản đã bị khóa. Vui lòng liên hệ chủ cửa hàng.", emailNhap, returnUrl);
                }
                return await DangNhapNhanVienAsync(nv, returnUrl);
            }

            // 2) Không phải nhân viên (hoặc cùng email nhưng khác mật khẩu) -> kiểm tra KHACHHANG
            var emailKh = emailNhap.ToLowerInvariant();
            var kh = emailKh.Length == 0 ? null
                : await _context.Khachhangs.FirstOrDefaultAsync(m => m.Email == emailKh);
            if (kh != null && TaiKhoanController.KiemTraMatKhau(_passwordHasher, kh, kh.MatKhau, matkhau))
                return await DangNhapKhachHangAsync(kh, returnUrl);

            // 3) Sai: thông báo chung, giữ lại email, không chuyển trang
            if (nv == null && kh == null)
                _passwordHasher.VerifyHashedPassword(new Khachhang(), HashGia, matkhau); // cân bằng thời gian xử lý
            return ViewLoi("Email hoặc mật khẩu không đúng.", emailNhap, returnUrl);
        }

        private IActionResult ViewLoi(string loi, string email, string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.email = email;
            ViewBag.LoiDangNhap = loi;
            return View();
        }

        private async Task<IActionResult> DangNhapNhanVienAsync(Nhanvien nv, string? returnUrl)
        {
            // Kết thúc phiên khách hàng cũ (Session) trước khi cấp cookie quản trị
            HttpContext.Session.Remove("khachhang");
            HttpContext.Session.Remove("makh");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, nv.MaNv.ToString()),
                new(ClaimTypes.Name, nv.Ten),
                new(ClaimTypes.Email, nv.Email ?? ""),
                new(ClaimTypes.Role, nv.VaiTro)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            var dich = DangNhapHelper.ChoNhanVien(Url, returnUrl);
            return dich != null ? Redirect(dich) : RedirectToAction("Index", "Home");
        }

        private async Task<IActionResult> DangNhapKhachHangAsync(Khachhang kh, string? returnUrl)
        {
            // Kết thúc phiên quản trị cũ (Cookie) trước khi lưu phiên khách hàng
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Remove(BanHangController.KeyGioQuay);

            HttpContext.Session.SetString("khachhang", kh.Email!);
            HttpContext.Session.SetInt32("makh", kh.MaKh);

            var dich = DangNhapHelper.ChoKhachHang(Url, returnUrl);
            return dich != null ? Redirect(dich) : RedirectToAction(nameof(Index));
        }

        /// <summary>Hồ sơ khách hàng: thông tin, địa chỉ giao hàng mặc định, đổi mật khẩu.</summary>
        public async Task<IActionResult> CustomerInfo()
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var kh = await _context.Khachhangs.AsNoTracking().Include(k => k.Diachis).FirstOrDefaultAsync(k => k.MaKh == MaKhDangNhap);
            if (kh == null) return Signout();
            ViewBag.SoDon = await _context.DonHangs.CountAsync(d => d.MaKh == kh.MaKh);
            ViewBag.DonDangXuLy = await _context.DonHangs.CountAsync(d => d.MaKh == kh.MaKh && d.TrangThai <= HangSo.TrangThaiDon.DangGiao);
            return View(kh);
        }

        [HttpPost]
        public async Task<IActionResult> CapNhatThongTin(string hoten, string dienthoai, string? diachi, string? phuongxa, string? quanhuyen, string? tinhthanh)
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var kh = await _context.Khachhangs.Include(k => k.Diachis).FirstOrDefaultAsync(k => k.MaKh == MaKhDangNhap);
            if (kh == null) return Signout();
            hoten = (hoten ?? "").Trim();
            dienthoai = (dienthoai ?? "").Trim();
            if (hoten.Length == 0 || hoten.Length > 100) { Loi("Họ tên không hợp lệ."); return RedirectToAction(nameof(CustomerInfo)); }
            if (!Regex.IsMatch(dienthoai, @"^0\d{9,10}$")) { Loi("Số điện thoại không hợp lệ."); return RedirectToAction(nameof(CustomerInfo)); }
            kh.Ten = hoten;
            kh.DienThoai = dienthoai;

            if (!string.IsNullOrWhiteSpace(diachi))
            {
                var dc = kh.Diachis.OrderByDescending(d => d.MacDinh).FirstOrDefault();
                if (dc == null) { dc = new Diachi { MaKh = kh.MaKh, MacDinh = 1 }; kh.Diachis.Add(dc); }
                dc.DiaChi1 = Cat(diachi, 100)!;
                dc.PhuongXa = Cat(phuongxa, 50);
                dc.QuanHuyen = Cat(quanhuyen, 50);
                dc.TinhThanh = Cat(tinhthanh, 50);
            }
            await _context.SaveChangesAsync();
            ThongBao("Đã cập nhật hồ sơ.");
            return RedirectToAction(nameof(CustomerInfo));
        }

        private static string? Cat(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > max ? s[..max] : s;
        }

        [HttpPost]
        public async Task<IActionResult> DoiMatKhau(string matkhaucu, string matkhaumoi, string xacnhan)
        {
            if (YeuCauDangNhap() is IActionResult r) return r;
            var kh = await _context.Khachhangs.FirstOrDefaultAsync(k => k.MaKh == MaKhDangNhap);
            if (kh == null) return Signout();
            if (!TaiKhoanController.KiemTraMatKhau(_passwordHasher, kh, kh.MatKhau, matkhaucu)) Loi("Mật khẩu hiện tại không đúng.");
            else if (string.IsNullOrEmpty(matkhaumoi) || matkhaumoi.Length < 6) Loi("Mật khẩu mới tối thiểu 6 ký tự.");
            else if (matkhaumoi != xacnhan) Loi("Xác nhận mật khẩu không khớp.");
            else
            {
                kh.MatKhau = _passwordHasher.HashPassword(kh, matkhaumoi);
                await _context.SaveChangesAsync();
                ThongBao("Đã đổi mật khẩu.");
            }
            return RedirectToAction(nameof(CustomerInfo));
        }

        public IActionResult Signout()
        {
            HttpContext.Session.Remove("khachhang");
            HttpContext.Session.Remove("makh");
            return RedirectToAction(nameof(Index));
        }
    }
}
