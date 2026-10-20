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
