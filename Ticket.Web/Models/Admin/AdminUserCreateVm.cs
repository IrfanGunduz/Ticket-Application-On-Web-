using System.ComponentModel.DataAnnotations;

namespace Ticket.Web.Models.Admin;

public sealed class AdminUserCreateVm
{
    [Required, StringLength(50)]
    public string UserName { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 4)]
    public string Password { get; set; } = "1234";

    // default: User rolü
    public bool IsAdmin { get; set; } = false;
}
