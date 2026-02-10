using System.ComponentModel.DataAnnotations;

namespace Ticket.Web.Models.Admin;

public sealed class AdminUserResetPasswordVm
{
    public Guid Id { get; set; }

    [Required, StringLength(100, MinimumLength = 4)]
    public string NewPassword { get; set; } = "1234";
}
