namespace Sales.Contracts;

public static class SalesPermissions
{
    public const string OrdersRead = "sales:order:read";
    public const string OrdersCreate = "sales:order:create";
    public const string OrdersCancel = "sales:order:cancel";
}
