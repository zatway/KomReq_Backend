namespace Platform.Helpers.UsersHelper;

public static class AddUserService
{
    public static string GetDescriptionByRoleName(string roleName)
    {
        switch (roleName)
        {
            case "Admin":
                return "Роль для выполнения действий администратора";
            case "Manager":
                return "Роль менеджера";
            default:
                return "";
        }
    }
}