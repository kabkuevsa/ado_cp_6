using System.ComponentModel.DataAnnotations;

namespace task6.Models
{
    public class UpdateEmailRequest
    {
        [Required(ErrorMessage = "Текущее имя обязательно")]
        public string CurrentName { get; set; }

        [Required(ErrorMessage = "Новый email обязателен")]
        [EmailAddress(ErrorMessage = "Некорректный email адрес")]
        public string NewEmail { get; set; }
    }
}