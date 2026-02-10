using System.ComponentModel.DataAnnotations;

namespace Ticket.Web.Models.Customers;

public sealed class CustomerEditVm
{
    public Guid? CustomerId { get; set; }

    [Required(ErrorMessage = "Kod zorunludur.")]
    [StringLength(50)]
    [Display(Name = "Kod")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Ünvan zorunludur.")]
    [StringLength(200)]
    [Display(Name = "Ünvan")]
    public string Title { get; set; } = "";

    [StringLength(50)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [StringLength(200)]
    [EmailAddress(ErrorMessage = "Email formatı geçersiz.")]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [StringLength(500)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }
}
