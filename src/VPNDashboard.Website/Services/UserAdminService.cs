using Microsoft.AspNetCore.Identity;

namespace VPNDashboard.Website.Services;

public class UserListItem
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsLockedOut { get; set; }
}

public class UserAdminService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    private static readonly string[] ValidRoles = ["Admin", "Viewer"];

    public UserAdminService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<List<UserListItem>> ListAsync()
    {
        var users = _userManager.Users.ToList();
        var items = new List<UserListItem>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItem
            {
                Id = user.Id,
                Email = user.Email ?? "(no email)",
                Role = roles.FirstOrDefault() ?? "Viewer",
                IsLockedOut = await _userManager.IsLockedOutAsync(user)
            });
        }

        return items;
    }

    public async Task<IdentityResult> CreateAsync(string email, string password, string role)
    {
        if (!ValidRoles.Contains(role))
            return IdentityResult.Failed(new IdentityError { Description = $"Invalid role: {role}" });

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return result;

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return roleResult;
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetRoleAsync(string userId, string newRole)
    {
        if (!ValidRoles.Contains(newRole))
            return IdentityResult.Failed(new IdentityError { Description = $"Invalid role: {newRole}" });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains("Admin") && newRole != "Admin")
        {
            var adminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;
            if (adminCount <= 1)
                return IdentityResult.Failed(new IdentityError { Description = "Cannot demote the last admin." });
        }

        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return removeResult;
        }

        return await _userManager.AddToRoleAsync(user, newRole);
    }

    public async Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
            return removeResult;

        return await _userManager.AddPasswordAsync(user, newPassword);
    }

    public async Task<IdentityResult> DeleteAsync(string userId, string currentUserId)
    {
        if (userId == currentUserId)
            return IdentityResult.Failed(new IdentityError { Description = "You cannot delete your own account." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin"))
        {
            var adminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;
            if (adminCount <= 1)
                return IdentityResult.Failed(new IdentityError { Description = "Cannot delete the last admin." });
        }

        return await _userManager.DeleteAsync(user);
    }

    public async Task<IdentityResult> ChangeOwnPasswordAsync(IdentityUser user, string currentPassword, string newPassword)
    {
        return await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
    }
}
