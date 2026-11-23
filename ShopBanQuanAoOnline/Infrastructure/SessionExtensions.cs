using System.Text.Json;

namespace ShopBanQuanAoOnline.Infrastructure;

public static class SessionExtensions
{
    public static void SetJson<T>(this ISession session, string key, T value) =>
        session.SetString(key, JsonSerializer.Serialize(value));

    public static T? GetJson<T>(this ISession session, string key)
    {
        var s = session.GetString(key);
        if (string.IsNullOrEmpty(s)) return default;
        try { return JsonSerializer.Deserialize<T>(s); }
        catch (JsonException) { return default; } // dữ liệu session cũ (định dạng trước khi nâng cấp) -> bỏ qua
    }
}
