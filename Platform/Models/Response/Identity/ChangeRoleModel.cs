namespace Platform.Models.Response.Identity;

public class ChangeRoleModel
{
    public string UserId { get; set; }
    public List<string> NewRoles { get; set; } = new();
}