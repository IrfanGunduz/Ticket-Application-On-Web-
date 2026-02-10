using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ticket.Infrastructure.Identity;
using Ticket.Web.Models.Account;

namespace Ticket.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _signInManager;

    public AccountController(SignInManager<AppUser> signInManager)
    {
        _signInManager = signInManager;
    }

   

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
        => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _signInManager.PasswordSignInAsync(
            vm.UserName, vm.Password, vm.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return LocalRedirect(vm.ReturnUrl ?? "/");

        ModelState.AddModelError("", "Kullanıcı adı veya şifre hatalı.");
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }
    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
