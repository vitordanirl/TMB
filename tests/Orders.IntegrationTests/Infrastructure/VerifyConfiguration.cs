using System.Runtime.CompilerServices;

namespace Orders.IntegrationTests.Infrastructure;

public static class VerifyConfiguration
{
    /// <summary>
    /// Golden tests: snapshots em <c>Snapshots/*.verified.*</c> (versionados). Valores voláteis
    /// (ids, datas, trace ids) são normalizados para que o snapshot só mude quando o contrato mudar.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.DerivePathInfo((sourceFile, projectDirectory, type, method) => new PathInfo(
            directory: Path.Combine(projectDirectory, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name));

        VerifierSettings.ScrubInlineGuids();
        VerifierSettings.IgnoreMember("trace_id");
        VerifierSettings.IgnoreMember("traceId");
        VerifierSettings.IgnoreMember("MT-Activity-Id"); // trace W3C propagado pelo MassTransit
    }
}
