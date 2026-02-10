using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Ticket.Infrastructure.Entities;

namespace Ticket.Web.Models.Tickets;

public sealed class TicketCreateVm
{
    [Display(Name = "Müşteri")]
    public Guid? CustomerId { get; set; }

    [Display(Name = "Sorun/Hizmet")]
    public Guid? ProblemId { get; set; }

    [Required(ErrorMessage = "Konu zorunludur.")]
    [StringLength(300)]
    [Display(Name = "Konu")]
    public string Subject { get; set; } = "";

    [StringLength(2000)]
    [Display(Name = "İlk not (opsiyonel)")]
    public string? InitialNote { get; set; }

    [Display(Name = "Kanal")]
    public TicketChannel Channel { get; set; } = TicketChannel.Manual;

    // Dropdown data
    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> Problems { get; set; } = new();
    public List<SelectListItem> ChannelOptions { get; set; } = new();

}
