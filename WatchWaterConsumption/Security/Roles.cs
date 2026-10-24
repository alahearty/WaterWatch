namespace WaterWatch.Security;

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string GisAnalyst = "GISAnalyst";
    public const string WaterUtilityEngineer = "WaterUtilityEngineer";
    public const string Hydrologist = "Hydrologist";
    public const string EnvironmentalOfficer = "EnvironmentalOfficer";
    public const string FieldInspector = "FieldInspector";
    public const string ManagementViewer = "ManagementViewer";
    public const string PublicViewer = "PublicViewer";

    public static readonly string[] All =
    {
        Administrator, GisAnalyst, WaterUtilityEngineer, Hydrologist,
        EnvironmentalOfficer, FieldInspector, ManagementViewer, PublicViewer
    };
}

public static class Policies
{
    public const string CanEditAssets = "CanEditAssets";
    public const string CanImportData = "CanImportData";
    public const string CanManageQuality = "CanManageQuality";
    public const string CanManageHydrology = "CanManageHydrology";
    public const string CanManageFieldOperations = "CanManageFieldOperations";
    public const string CanManageNrw = "CanManageNrw";
    public const string CanManageTelemetry = "CanManageTelemetry";
    public const string CanAdminister = "CanAdminister";
    public const string CanViewSensitive = "CanViewSensitive";
    public const string CanViewTelemetry = "CanViewTelemetry";

    public static readonly (string Name, string[] Roles)[] Definitions =
    {
        (CanEditAssets, new[] { Roles.Administrator, Roles.GisAnalyst, Roles.WaterUtilityEngineer }),
        (CanImportData, new[] { Roles.Administrator, Roles.GisAnalyst }),
        (CanManageQuality, new[] { Roles.Administrator, Roles.EnvironmentalOfficer }),
        (CanManageHydrology, new[] { Roles.Administrator, Roles.Hydrologist }),
        (CanManageFieldOperations, new[] { Roles.Administrator, Roles.FieldInspector, Roles.WaterUtilityEngineer }),
        (CanManageNrw, new[] { Roles.Administrator, Roles.WaterUtilityEngineer }),
        (CanManageTelemetry, new[] { Roles.Administrator, Roles.WaterUtilityEngineer, Roles.Hydrologist }),
        (CanAdminister, new[] { Roles.Administrator }),
        (CanViewSensitive, new[] { Roles.Administrator, Roles.GisAnalyst, Roles.WaterUtilityEngineer }),
        (CanViewTelemetry, Roles.All)
    };
}
