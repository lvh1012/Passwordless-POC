using MagicLinkAuthn.Data;
using MagicLinkAuthn.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicLinkAuthn.Pages;

public sealed class OnboardingModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserProfileService _profileService;

    public OnboardingModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        UserProfileService profileService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _profileService = profileService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (await _profileService.IsCompleteAsync(userId, cancellationToken))
        {
            return LocalRedirect(UserProfileService.GetSafeReturnUrl(ReturnUrl));
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        var result = await _profileService.CompleteAsync(
            userId,
            Input.FullName,
            Input.PhoneNumber,
            cancellationToken);

        if (result.Status == UserProfileCompletionStatus.Invalid)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError($"Input.{error.Key}", error.Value);
            }

            return Page();
        }

        if (result.Status == UserProfileCompletionStatus.UserNotFound)
        {
            await _signInManager.SignOutAsync();
            return Redirect("/");
        }

        return LocalRedirect(UserProfileService.GetSafeReturnUrl(ReturnUrl));
    }

    public sealed class InputModel
    {
        public string? FullName { get; set; }

        public string? PhoneNumber { get; set; }
    }
}
