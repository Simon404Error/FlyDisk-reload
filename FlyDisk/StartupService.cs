// Copyright (C) 2026 youke1686 (https://github.com/youke1686)
//
// This file is part of FlyDisk.
//
// FlyDisk is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// FlyDisk is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with FlyDisk.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using FlyDisk.Engine;
using FlyDisk.Localization;
using FlyDisk.Models;

namespace FlyDisk
{
    /// <summary>
    /// 开机自启与「开机保持脱机」的**计划任务**管理（见 AGENTS.md §2.7）。
    ///
    /// **为什么不用注册表 Run 键**：本程序是 <c>requireAdministrator</c>（整盘脱机 + 独占 PhysicalDrive 必需），
    /// Run 键在用户登录时启动需要提升权限 ⇒ 每次开机都会弹一次 UAC（或被系统静默拒绝）。
    /// 计划任务可以声明"以最高权限运行"，由系统直接以提升权限拉起，**不弹 UAC**。
    ///
    /// **两条任务，各管一件事**（"无需登录也要脱机" + "登录后要看到界面"不可能由同一个进程完成：
    /// 以 SYSTEM 在无人登录时启动的进程，它的窗口不会出现在任何用户桌面上）：
    ///
    /// | 任务 | 触发器 | 身份 | 命令 | 作用 |
    /// | --- | --- | --- | --- | --- |
    /// | <see cref="KeepOfflineTaskName"/> | 开机（<c>BootTrigger</c>，延迟 1 分钟） | <c>SYSTEM</c> | `FlyDisk.exe --keep-offline-only` | 把「开机目标盘」脱机后**立刻退出**，不显示界面 |
    /// | <see cref="StartupTaskName"/> | 用户登录（<c>LogonTrigger</c>） | 创建任务时的当前用户 | `FlyDisk.exe` | 带界面启动本程序（登陆后可见），并在启动期再幂等确认一次脱机 |
    ///
    /// 两条都设 <c>MultipleInstancesPolicy = IgnoreNew</c>：登录触发时若开机那条还在跑，直接忽略，
    /// 不会出现两个实例（计划任务层面就挡住了，比在程序里加互斥体更早）。
    ///
    /// 「保持脱机」能跨进程生效的原因：脱机属性是 <c>persist = true</c> 写进系统的，
    /// 进程退出后盘仍然脱机（与「停止加速后盘保持脱机」同一机制，见 <see cref="PhysicalDiskHandle"/> 类注释）。
    /// </summary>
    internal static class StartupService
    {
        /// <summary>开机（无需登录）保持脱机的任务名（带顶级文件夹，避免与用户自建任务混在一起）</summary>
        public const string KeepOfflineTaskName = @"\FlyDisk\KeepOffline";

        /// <summary>登录后带界面启动本程序的任务名</summary>
        public const string StartupTaskName = @"\FlyDisk\AutoStart";

        /// <summary>命令行开关：只做「保持脱机」，不建界面（见 <see cref="Program"/>）</summary>
        public const string KeepOfflineOnlySwitch = "--keep-offline-only";

        /// <summary>开机任务相对系统启动的延迟：留出存储枚举 / 卷管理器就绪的时间</summary>
        private const string BootTriggerDelay = "PT1M";

        /// <summary>schtasks 的单次调用超时（毫秒）：任务计划服务没响应时不要卡住界面</summary>
        private const int SchtasksTimeoutMs = 30000;

        /// <summary>
        /// 【设置窗口保存时】按开关**强制**创建或删除两条任务。
        /// 失败**不吞**：返回 false 并把可读原因交给调用方弹窗（与 <see cref="ConfigService.Save"/> 同口径）。
        /// </summary>
        public static bool Apply(bool runAtStartup, bool keepOfflineOnBoot, out string error)
            => Sync(runAtStartup, keepOfflineOnBoot, force: true, out error);

        /// <summary>
        /// 【每次程序启动时】校正任务状态：开关为开而任务缺失 / 指向的不是当前 exe（exe 被移动或改名）⇒ 重建；
        /// 开关为关而任务还在 ⇒ 删除。已一致时**不调用 schtasks**，所以正常启动没有额外开销。
        /// </summary>
        public static bool Ensure(bool runAtStartup, bool keepOfflineOnBoot, out string error)
            => Sync(runAtStartup, keepOfflineOnBoot, force: false, out error);

        private static bool Sync(bool runAtStartup, bool keepOfflineOnBoot, bool force, out string error)
        {
            error = string.Empty;
            string exePath = CurrentExePath;

            if (!SyncOfflineTask(keepOfflineOnBoot, force, exePath, out string offlineError)) error = offlineError;
            if (!SyncStartupTask(runAtStartup, force, exePath, out string startupError))
            {
                error = error.Length == 0 ? startupError : error + Environment.NewLine + startupError;
            }
            return error.Length == 0;
        }

        /// <summary>当前 exe 的完整路径（计划任务里写的必须是它）</summary>
        private static string CurrentExePath
        {
            get
            {
                // ProcessPath 比 Assembly.Location 可靠：单文件发布（PublishSingleFile）下后者是空串
                string? path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path)) return path;
                return Path.Combine(AppContext.BaseDirectory, "FlyDisk.exe");
            }
        }

        private static bool SyncOfflineTask(bool wanted, bool force, string exePath, out string error)
        {
            error = string.Empty;
            bool exists = TaskExists(KeepOfflineTaskName);
            if (!wanted)
            {
                if (!exists) return true;   // 本就不该有，且没有：无需动
                return DeleteTask(KeepOfflineTaskName, out error);
            }

            if (!force && exists && TaskPointsTo(KeepOfflineTaskName, exePath)) return true;
            return CreateTask(KeepOfflineTaskName, BuildOfflineTaskXml(exePath), out error);
        }

        private static bool SyncStartupTask(bool wanted, bool force, string exePath, out string error)
        {
            error = string.Empty;
            bool exists = TaskExists(StartupTaskName);
            if (!wanted)
            {
                if (!exists) return true;
                return DeleteTask(StartupTaskName, out error);
            }

            if (!force && exists && TaskPointsTo(StartupTaskName, exePath)) return true;
            return CreateTask(StartupTaskName, BuildStartupTaskXml(exePath), out error);
        }

        /// <summary>
        /// 开机任务：<c>SYSTEM</c> + 开机后 1 分钟 + 只跑 <c>--keep-offline-only</c>。
        /// 用 SYSTEM 是"无需登录"的必要条件；该分支不建窗体，所以不会出现"窗口在会话 0 里没人看得见"的问题。
        /// Principal 必须显式写 <c>ServiceAccount</c>：缺省是 <c>InteractiveToken</c>，对 <c>S-1-5-18</c> 无效、任务起不来。
        /// </summary>
        private static string BuildOfflineTaskXml(string exePath) => $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>FlyDisk</Author>
                <Description>FlyDisk：开机把选中的硬盘保持脱机（无需登录；做完即退出）</Description>
              </RegistrationInfo>
              <Triggers>
                <BootTrigger>
                  <Enabled>true</Enabled>
                  <Delay>{BootTriggerDelay}</Delay>
                </BootTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>S-1-5-18</UserId>
                  <LogonType>ServiceAccount</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT1H</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{Escape(exePath)}</Command>
                  <Arguments>{KeepOfflineOnlySwitch}</Arguments>
                  <WorkingDirectory>{Escape(Path.GetDirectoryName(exePath) ?? string.Empty)}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;

        /// <summary>
        /// 登录任务：<c>InteractiveToken</c> + 当前用户 + 最高权限 ⇒ 登录即起、界面正常显示、不弹 UAC。
        /// 这里不写 <c>UserId</c> 的 SID 而是写"域\用户名"，因为任务要跑在**用户自己的会话**里。
        /// </summary>
        private static string BuildStartupTaskXml(string exePath) => $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>FlyDisk</Author>
                <Description>FlyDisk：登录后带界面启动本程序（最高权限，无 UAC 提示）</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{Escape(CurrentUserName)}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{Escape(CurrentUserName)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{Escape(exePath)}</Command>
                  <WorkingDirectory>{Escape(Path.GetDirectoryName(exePath) ?? string.Empty)}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;

        /// <summary>当前用户名（`域\用户`）；取不到时用 <c>Environment.UserName</c>（任务计划也接受纯用户名）</summary>
        private static string CurrentUserName
        {
            get
            {
                string domain = Environment.UserDomainName;
                string user = Environment.UserName;
                return string.IsNullOrEmpty(domain) ? user : domain + "\\" + user;
            }
        }

        private static bool TaskExists(string taskName)
        {
            int rc = RunSchtasks(new[] { "/Query", "/TN", taskName }, out _, out _);
            return rc == 0;
        }

        /// <summary>
        /// 任务是否指向当前 exe。`schtasks /Query /XML` 的输出**可能是 UTF-16**，
        /// 经重定向被按 UTF-8 解码时字符之间会夹 <c>\0</c> ⇒ 先去掉 NUL 再匹配。
        /// </summary>
        private static bool TaskPointsTo(string taskName, string exePath)
        {
            int rc = RunSchtasks(new[] { "/Query", "/TN", taskName, "/XML" }, out string stdout, out _);
            if (rc != 0) return false;

            string text = stdout.Replace("\0", string.Empty);
            return text.Contains(exePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CreateTask(string taskName, string xml, out string error)
        {
            error = string.Empty;
            string? directory = Path.GetDirectoryName(ServiceConstants.ConfigFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                error = Locale.T("startup.task.createFailed", taskName, -1, "数据目录不可用");
                return false;
            }

            try
            {
                Directory.CreateDirectory(directory);
                // schtasks /XML 要求文件与 XML 声明一致：这里声明 UTF-16，就按 UTF-16 落盘
                string file = Path.Combine(directory, taskName.TrimStart('\\').Replace('\\', '_') + ".xml");
                File.WriteAllText(file, xml, Encoding.Unicode);

                int rc = RunSchtasks(new[] { "/Create", "/TN", taskName, "/XML", file, "/F" },
                    out _, out string stderr);
                if (rc == 0) return true;

                error = Locale.T("startup.task.createFailed", taskName, rc, stderr.Trim());
                LogService.DebugFile($"计划任务创建失败：{taskName} rc={rc} {stderr.Trim()}");
                return false;
            }
            catch (Exception ex)
            {
                error = Locale.T("startup.task.createFailed", taskName, -1, ex.Message);
                LogService.DebugFile($"计划任务创建异常：{taskName} {ex.Message}");
                return false;
            }
        }

        private static bool DeleteTask(string taskName, out string error)
        {
            error = string.Empty;
            int rc = RunSchtasks(new[] { "/Delete", "/TN", taskName, "/F" }, out _, out string stderr);
            if (rc == 0 || !TaskExists(taskName)) return true;   // 已经没了也算成功（幂等）

            error = Locale.T("startup.task.deleteFailed", taskName, rc, stderr.Trim());
            LogService.DebugFile($"计划任务删除失败：{taskName} rc={rc} {stderr.Trim()}");
            return false;
        }

        /// <summary>调一次 schtasks.exe，返回退出码（失败返回 -1）。输出走重定向，不弹黑窗。</summary>
        private static int RunSchtasks(IReadOnlyList<string> arguments, out string stdout, out string stderr)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            try
            {
                var startInfo = new ProcessStartInfo("schtasks.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

                using Process? process = Process.Start(startInfo);
                if (process == null) return -1;

                stdout = process.StandardOutput.ReadToEnd();
                stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(SchtasksTimeoutMs))
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* 杀不掉只能作罢 */ }
                    LogService.DebugFile($"schtasks 超时（{SchtasksTimeoutMs} ms）：{string.Join(' ', arguments)}");
                    return -1;
                }
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                LogService.DebugFile($"schtasks 调用失败：{string.Join(' ', arguments)} ⇒ {ex.Message}");
                return -1;
            }
        }

        /// <summary>XML 文本转义（用户名的域/账户理论上可能含 <c>&amp;</c> 等字符）</summary>
        private static string Escape(string text)
            => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                   .Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}
