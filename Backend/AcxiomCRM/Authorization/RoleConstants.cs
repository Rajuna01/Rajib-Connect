
namespace AcxiomCRM.Authorization;

public static class RoleConstants
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";

    public const string AdminOrManager =
        Admin + "," + Manager;

    public const string AllRoles =
        Admin + "," + Manager + "," + SalesExecutive;
}
