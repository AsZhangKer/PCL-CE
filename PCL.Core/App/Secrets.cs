using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.App;

// ReSharper disable InconsistentNaming
public static class Secrets
{
    /// <summary>
    /// 微软 OAuth 的 Client ID
    /// </summary>
    public static string MSOAuthClientId
    {
        get
        {
            // 用户通过设置自定义的 Client ID 优先（用于已获 Minecraft API 权限的客户端）
            var overridden = Config.Preference.Theme.MsClientId?.Trim();
            if (!string.IsNullOrEmpty(overridden))
                return overridden;
            // Client ID 属于 OAuth 公共客户端标识（非机密），因此允许 Release 构建也从运行时环境变量读取，
            // 便于未打包时通过 .env.local / 系统环境变量注入，而不必重新编译。
            return EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: false).ReplaceNullOrEmpty();
        }
    }

    /// <summary>
    /// 旧版 Live 认证流程使用的 Client ID。<br/>
    /// 默认值取自 Nintendo Switch 版的公共 Client ID（与 azalea 等开源项目所用一致），
    /// 可通过环境变量 PCL_MS_LEGACY_CLIENT_ID 覆盖。<br/>
    /// 仅在 <see cref="IsMsLegacyFlow"/> 为 true 时使用。
    /// </summary>
    public static string MSLegacyClientId { get; } =
        EnvironmentInterop.GetSecret("MS_LEGACY_CLIENT_ID", readEnvDebugOnly: true) is { Length: > 0 } custom
            ? custom
            : "00000000441cc96b";

    /// <summary>
    /// 是否启用旧版 Live 认证流程（临时借用公共 Client ID 绕过 Mojang 白名单审核）。
    /// </summary>
    public static bool IsMsLegacyFlow => Config.Launch.LoginMsLegacyShell;

    /// <summary>
    /// 当前认证流程实际使用的 Client ID。
    /// </summary>
    public static string MSActiveClientId => IsMsLegacyFlow ? MSLegacyClientId : MSOAuthClientId;

    /// <summary>
    /// CurseForge API 的 Client ID
    /// </summary>
    public static string CurseForgeAPIKey { get; } = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 遥测密钥
    /// </summary>
    public static string TelemetryKey { get; } = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 当前版本的 Git 提交 SHA
    /// </summary>
    public static string CommitHash { get; } = EnvironmentInterop.GetSecret("GITHUB_SHA", readEnvDebugOnly: true).ReplaceNullOrEmpty();
}
