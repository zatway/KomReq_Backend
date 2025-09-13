namespace Platform.Models.Response.Identity;

public class ChangePasswordModel
{
    public string UserId { get; set; }
    public string CurrentPassword { get; set; }
    public string NewPassword { get; set; }
}