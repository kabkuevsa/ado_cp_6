using System.ComponentModel.DataAnnotations;

namespace task6.Models
{
    public class UpdateUserRequest
    {
        [Required(ErrorMessage = "Имя обязательно")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно быть от 2 до 100 символов")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Некорректный email адрес")]
        public string Email { get; set; }

        [Range(0, 150, ErrorMessage = "Возраст должен быть от 0 до 150 лет")]
        public int? Age { get; set; }
    }
}