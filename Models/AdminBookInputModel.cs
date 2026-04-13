using System.ComponentModel.DataAnnotations;

namespace BookStore.Models;

public class AdminBookInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên sách")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tác giả")]
    public string Author { get; set; } = string.Empty;

    [Range(0, 100000000, ErrorMessage = "Giá không hợp lệ")]
    public decimal Price { get; set; }

    [Range(0, 1000000, ErrorMessage = "Tồn kho không hợp lệ")]
    public int Stock { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục")]
    public int CategoryId { get; set; }

    public string Description { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
}



