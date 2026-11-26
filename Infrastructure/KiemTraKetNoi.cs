using System.Net;
using Microsoft.Data.SqlClient;

namespace ShopBanQuanAoOnline.Infrastructure;

/// <summary>
/// Hỗ trợ cấu hình SQL Server khi chạy trên máy khác:
/// - In ra console tên server / database đang dùng (đọc từ appsettings.json).
/// - Nếu không kết nối được, hiển thị trang hướng dẫn tiếng Việt nêu rõ nguyên nhân thay cho trang lỗi chung.
/// </summary>
public static class KiemTraKetNoi
{
    public static (string Server, string Database) DocThongTin(string connectionString)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            return (b.DataSource, b.InitialCatalog);
        }
        catch (ArgumentException)
        {
            return ("(chuỗi kết nối sai định dạng)", "");
        }
    }

    private static SqlException? TimLoiSql(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is SqlException s) return s;
            ex = ex.InnerException;
        }
        return null;
    }

    private static bool LaLoiKetNoi(SqlException ex) =>
        ex.Number is -1 or 0 or 2 or 26 or 40 or 53 or 121 or 10060 or 10061 or 11001
        || ex.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("instance", StringComparison.OrdinalIgnoreCase);

    private static string NguyenNhan(SqlException ex, string server, string database) => ex.Number switch
    {
        4060 => $"Đã kết nối được SQL Server <b>{server}</b> nhưng chưa có database <b>{database}</b>. Hãy mở SSMS và chạy file <code>SQL/ShopBanQuanAoOnline.sql</code>.",
        18456 => $"SQL Server <b>{server}</b> từ chối đăng nhập. Nếu dùng tài khoản Windows, giữ <code>Trusted_Connection=True</code>; nếu dùng tài khoản SQL, kiểm tra <code>User Id</code>/<code>Password</code>.",
        208 => $"Database <b>{database}</b> thiếu bảng mới. Hãy chạy file <code>SQL/CapNhat_ShopBanQuanAoOnline.sql</code> (giữ dữ liệu) hoặc <code>SQL/ShopBanQuanAoOnline.sql</code> (tạo lại).",
        207 => $"Database <b>{database}</b> thiếu cột mới. Hãy chạy file <code>SQL/CapNhat_ShopBanQuanAoOnline.sql</code>.",
        _ when !LaLoiKetNoi(ex) => $"Lỗi cơ sở dữ liệu: {WebUtility.HtmlEncode(ex.Message)}<br>Nếu vừa cập nhật mã nguồn, hãy chạy <code>SQL/CapNhat_ShopBanQuanAoOnline.sql</code> để CSDL có đủ bảng/cột mới.",
        _ => $"Không tìm thấy hoặc không mở được SQL Server <b>{server}</b>. Kiểm tra tên server (xem ô <i>Server name</i> khi đăng nhập SSMS) và dịch vụ <i>SQL Server</i> đang chạy (Services.msc)."
    };

    /// <summary>Middleware bắt lỗi kết nối / thiếu bảng và trả về trang hướng dẫn.</summary>
    public static IApplicationBuilder UseHuongDanKetNoiSql(this IApplicationBuilder app, string connectionString)
    {
        var (server, database) = DocThongTin(connectionString);
        return app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex) when (TimLoiSql(ex) is SqlException sql && !ctx.Response.HasStarted)
            {
                var s = WebUtility.HtmlEncode(server);
                var d = WebUtility.HtmlEncode(database);
                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.WriteAsync($$"""
                    <!DOCTYPE html><html lang="vi"><head><meta charset="utf-8"><title>Chưa kết nối được CSDL</title>
                    <style>body{font-family:Segoe UI,Arial,sans-serif;max-width:760px;margin:40px auto;padding:0 16px;color:#222}
                    code,pre{background:#f3f3f3;padding:2px 6px;border-radius:4px}pre{padding:12px;overflow:auto}
                    .box{border-left:4px solid #d9480f;background:#fff4ee;padding:12px 16px;margin:16px 0}</style></head><body>
                    <h2>Website chưa kết nối được cơ sở dữ liệu</h2>
                    <div class="box">{{NguyenNhan(sql, s, d)}}</div>
                    <p>Cấu hình hiện tại: Server = <code>{{s}}</code>, Database = <code>{{d}}</code></p>
                    <h3>Cách sửa</h3>
                    <ol>
                      <li>Mở file <code>ShopBanQuanAoOnline/appsettings.json</code>.</li>
                      <li>Sửa phần <code>Server=...</code> thành tên SQL Server trên máy bạn, ví dụ:
                        <pre>"DefaultConnection": "Server=.\\SQLEXPRESS;Database=ShopBanQuanAoOnline;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"</pre>
                        Trong file JSON, dấu <code>\</code> phải viết thành <code>\\</code>.</li>
                      <li>Lưu file, dừng website và chạy lại (F5).</li>
                    </ol>
                    <p style="color:#777;font-size:13px">Mã lỗi SQL: {{sql.Number}}</p>
                    </body></html>
                    """);
            }
        });
    }
}
