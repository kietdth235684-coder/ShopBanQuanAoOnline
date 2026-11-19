# Website quản lý bán quần áo MIKA – ShopBanQuanAoOnline

ASP.NET Core MVC (.NET 8) + Entity Framework Core 8 + Microsoft SQL Server.
Gồm 2 khu vực tách biệt:

| Khu vực | Đường dẫn | Ai dùng |
|---|---|---|
| Website khách hàng | `/` (Customers) | Khách xem hàng, giỏ hàng, đặt COD, theo dõi đơn, đánh giá |
| Khu quản trị | `/Home` (đăng nhập tại `/TaiKhoan/DangNhap`) | Chủ cửa hàng, nhân viên |

## 1. Yêu cầu trên máy chạy

- Visual Studio 2022 (17.8 trở lên) có workload **ASP.NET and web development**, hoặc .NET 8 SDK.
- SQL Server 2016 trở lên (Express / Developer đều được) + SQL Server Management Studio (SSMS).
- Máy có Internet ở lần build đầu tiên để Visual Studio tải các gói NuGet (EF Core 8.0.14).

## 2. Tạo cơ sở dữ liệu

Thư mục `SQL/` có 2 file, **chỉ chạy 1 trong 2**:

| File | Khi nào dùng |
|---|---|
| `ShopBanQuanAoOnline.sql` | Máy mới / muốn làm lại từ đầu. **Xóa và tạo lại** database `ShopBanQuanAoOnline` kèm dữ liệu mẫu. |
| `CapNhat_ShopBanQuanAoOnline.sql` | Máy đã có database cũ của nhóm và muốn **giữ dữ liệu**. Chỉ thêm bảng/cột, chạy lại nhiều lần không lỗi. Nên Backup trước. |

Cách chạy: mở SSMS → kết nối vào SQL Server → `File > Open > File...` chọn file → bấm **Execute (F5)**.

## 3. Cấu hình SQL Server trên máy của bạn (chỉ sửa 1 chỗ)

Mã nguồn và script SQL **không chứa tên máy hay tên server nào**. Chỉ cần sửa đúng 1 dòng trong
`ShopBanQuanAoOnline/appsettings.json`:

```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=ShopBanQuanAoOnline;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

**Cách tìm tên server trên laptop:** mở SSMS, ô *Server name* trong hộp thoại *Connect to Server* chính là tên cần điền.

| Ô Server name trong SSMS hiện | Điền vào `Server=` trong appsettings.json |
|---|---|
| `LAPTOP-ABC123\SQLEXPRESS` | `.\\SQLEXPRESS` hoặc `LAPTOP-ABC123\\SQLEXPRESS` |
| `LAPTOP-ABC123` (bản Developer, instance mặc định) | `.` hoặc `localhost` |
| `LAPTOP-ABC123\SQLEXPRESS01` | `.\\SQLEXPRESS01` |
| `(localdb)\MSSQLLocalDB` | `(localdb)\\MSSQLLocalDB` |

Lưu ý:
- Trong file JSON, mỗi dấu `\` phải viết thành `\\`.
- `.` nghĩa là "máy đang chạy", nên `.\\SQLEXPRESS` dùng được trên mọi máy có SQL Express mặc định mà không cần biết tên laptop.
- Giữ `Database=ShopBanQuanAoOnline` (trùng tên trong script SQL).
- Dùng tài khoản SQL thay vì Windows: thay `Trusted_Connection=True` bằng `User Id=sa;Password=matkhau`.
- Dự án **không dùng User Secrets** nữa, nên không có cấu hình ẩn nào đè lên `appsettings.json`.

**Kiểm tra nhanh:** khi chạy, cửa sổ console in dòng `SQL Server: ... | Database: ...` cho biết đang dùng server nào.
Nếu điền sai tên, chưa bật dịch vụ SQL Server hoặc chưa chạy script, website hiện trang
**"Website chưa kết nối được cơ sở dữ liệu"** nêu nguyên nhân và cách sửa (thay cho trang lỗi chung).

Ngưỡng cảnh báo tồn kho thấp cũng chỉnh trong `appsettings.json` → `"Kho": { "NguongTonThap": 5 }`.

## 4. Chạy website

1. Mở `ShopBanQuanAoOnline.sln` bằng Visual Studio.
2. Chờ Visual Studio restore NuGet (hoặc chuột phải Solution → *Restore NuGet Packages*).
3. Bấm **F5** (profile `https` hoặc `http`). Trình duyệt mở trang khách hàng.
4. Vào khu quản trị: `https://localhost:7003/TaiKhoan/DangNhap` (hoặc link "Đăng nhập nhân viên" ở chân trang).

Chạy bằng dòng lệnh: `cd ShopBanQuanAoOnline` → `dotnet run`.

## 5. Tài khoản mẫu (mật khẩu đều là `123456`)

| Vai trò | Email | Ghi chú |
|---|---|---|
| Chủ cửa hàng | `chu@mika.vn` | Toàn quyền, xem báo cáo, quản lý nhân viên |
| Nhân viên | `nv@mika.vn` | Bán hàng, xử lý đơn COD, kho, duyệt đánh giá, chấm công |
| Khách hàng | `khach@gmail.com` | Đăng nhập trên website; đã có 1 hóa đơn mua áo Polo (được đánh giá) và 1 đơn COD chờ xác nhận |

Script cập nhật chỉ tạo `chu@mika.vn` khi database chưa có chủ cửa hàng. Đổi mật khẩu ngay sau lần đăng nhập đầu (menu tên người dùng → *Đổi mật khẩu*).

## 6. Sao lưu / phục hồi dữ liệu

SSMS → chuột phải database `ShopBanQuanAoOnline` → *Tasks > Back Up...* (tạo file `.bak`).
Phục hồi: chuột phải *Databases* → *Restore Database...* → chọn file `.bak`.

## 7. Cấu trúc mã nguồn

```
ShopBanQuanAoOnline/
├─ Program.cs                 Cấu hình DI, Session, đăng nhập cookie nhân viên, phân quyền
├─ appsettings.json           Chuỗi kết nối + ngưỡng tồn kho thấp
├─ Data/ApplicationDbContext  EF Core DbContext (database-first, khớp script SQL)
├─ Models/                    Thực thể (bảng) + HangSo.cs (trạng thái đơn, vai trò...) + ViewModels
├─ Services/                  Nghiệp vụ: KhoService, DonHangService (COD), BanHangService (quầy + đổi trả),
│                             DanhGiaService, BaoCaoService
├─ Infrastructure/            AdminController (bắt buộc đăng nhập), PhanQuyen, Session helper
├─ Controllers/               Mỗi controller ghi chú thuộc module nào
└─ Views/                     _LayoutAdmin (quản trị) / _LayoutCustomer (website)
```
