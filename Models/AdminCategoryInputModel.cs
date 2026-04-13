using System.ComponentModel.DataAnnotations;

namespace BookStore.Models;

public class AdminCategoryInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
    [MaxLength(100, ErrorMessage = "Tên danh mục tối đa 100 ký tự")]
    public string Name { get; set; } = string.Empty;
}



