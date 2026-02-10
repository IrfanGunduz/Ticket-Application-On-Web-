using System.ComponentModel.DataAnnotations;

namespace Ticket.Web.Models.Problems;

public sealed class ProblemEditVm
{
    public Guid? ProblemId { get; set; }

    [Required(ErrorMessage = "Kod zorunludur.")]
    [StringLength(50)]
    [Display(Name = "Kod")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Ad zorunludur.")]
    [StringLength(200)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Birim zorunludur.")]
    [StringLength(100)]
    [Display(Name = "Birim")]
    public string Department { get; set; } = "";
}
