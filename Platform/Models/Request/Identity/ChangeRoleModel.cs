namespace Platform.Models.Request.Identity;

public class ChangeRoleModel
{
    public string UserId { get; set; }
    public string NewRole { get; set; }
}