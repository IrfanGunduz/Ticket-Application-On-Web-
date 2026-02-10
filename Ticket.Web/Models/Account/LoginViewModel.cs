using System.ComponentModel.DataAnnotations;

namespace Ticket.Web.Models.Account;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Kullanıcı adı")]
    public string UserName { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}
