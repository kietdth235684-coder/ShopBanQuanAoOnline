namespace ShopBanQuanAoOnline.Models
{
    /// <summary>Một dòng giỏ hàng (lưu trong Session). Chỉ lưu thông tin cần hiển thị; giá được đọc lại từ CSDL khi đặt hàng.</summary>
    public class CartItem
    {
        public int MaBt { get; set; }
        public int MaMh { get; set; }
        public string Ten { get; set; } = "";
        public string KichCo { get; set; } = "";
        public string MauSac { get; set; } = "";
        public string? HinhAnh { get; set; }
        public int DonGia { get; set; }
        public int SoLuong { get; set; }
        public int ThanhTien => DonGia * SoLuong;
    }
}
