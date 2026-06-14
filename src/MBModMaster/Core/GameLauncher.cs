using System.Diagnostics;
using System.IO;
using MBModMaster.Models;

namespace MBModMaster.Core;

public sealed class GameLauncher
{
    public void LaunchDirect(string gameDirectory, IReadOnlyList<BannerlordModule> modules)
    {
        var executablePath = Path.Combine(gameDirectory, "bin", "Win64_Shipping_Client", "Bannerlord.exe");
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("未找到游戏主程序。", executablePath);
        }

        var enabledModules = modules
            .Where(module => module.IsSinglePlayerCompatible && module.IsEnabled)
            .OrderBy(module => module.LoadOrder ?? int.MaxValue)
            .Select(module => module.Id)
            .ToList();

        if (enabledModules.Count == 0)
        {
            throw new InvalidOperationException("没有可启动的启用模块。");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("/singleplayer");
        startInfo.ArgumentList.Add(BuildModulesArgument(enabledModules));

        Process.Start(startInfo);
    }

    public static string BuildModulesArgument(IReadOnlyList<string> moduleIds)
    {
        return $"_MODULES_*{string.Join('*', moduleIds)}*_MODULES_";
    }
}
