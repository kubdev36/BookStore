using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BookStore.Models;

public class AddReviewInputModel
{
    [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5")]
    public int Rating { get; set; } = 5;

    [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá")]
    [MaxLength(1500, ErrorMessage = "Nội dung tối đa 1500 ký tự")]
    public string Comment { get; set; } = string.Empty;

    public IFormFile? ImageFile { get; set; }
}
