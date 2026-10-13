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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using FlyDisk.Engine;
using FlyDisk.Localization;
using FlyDisk.Models;
using FlyDisk.Theming;

namespace FlyDisk
{
    /// <summary>
    /// 设置对话框：iSCSI 监听端口 + 两级缓存参数。
    ///
    /// **2026-10-01 的改动：这里不再选目标盘**。选盘改成"每次点「启动加速」时弹一次"
    /// （见 后续待办.md 第一节的"选盘时机"），目的是防止"改过配置后隔几天才启动、忘了当时选的是哪块"。
    /// 于是本对话框**不再分形态**：远程形态下同样用它配缓存参数（客户端照样要装缓存）。
    ///
    /// 配置在**启动那一刻**被读取，所以主界面在运行时会锁住入口。
    /// </summary>
    public partial class Settings : ThemedForm, ILocalizable
    {
        private readonly DiskConfig _config;

        /// <summary>控件登记表（见 <see cref="ILocalizable"/>）：静态文本键 → 控件</summary>
        public Dictionary<string, List<Control>> TextBindings { get; } = new();

        public Settings(DiskConfig config)
        {
            InitializeComponent();
            _config = config;

            // 静态文本全部登记在这里（Designer 里的中文初值随后被覆盖）；动态文本走 ApplyDynamicText
            Bind("settings.enableMemoryCache", chkEnableCache);
            Bind("settings.eviction", lblEviction);
            Bind("settings.stop", lblStop);
            Bind("settings.save", btnSave);
            Bind("settings.docs", btnDocs);
            Bind("settings.groupDisk", groupBoxDisk);
            Bind("settings.listenPort", label2);
            Bind("settings.groupCache", groupBoxCache);
            Bind("settings.groupSsd", groupBoxSsd);
            Bind("settings.enableSsd", chkEnableSsdCache);
            Bind("settings.ssdDrive", lblSsdDrive);
            Bind("settings.ssdSize", lblSsdSize);
            Bind("settings.ssdConservative", lblSsdConservative);
            Bind("settings.l2Manage", btnL2Manage);
            Bind("settings.groupStartup", groupBoxStartup);
            Bind("settings.runAtStartup", chkRunAtStartup);
            Bind("settings.keepOfflineOnBoot", chkKeepOfflineOnBoot);
            Bind("settings.driveLetters", lblDriveLetters);
            Bind("settings.addByDrive", btnAddByDrive);
            Bind("settings.pickDisks", btnPickStartupDisks);

            LocalizationManager.Apply(this);
        }

        /// <summary>登记一条「键 → 控件」（一条键可绑多个控件）</summary>
        private void Bind(string key, params Control[] controls)
        {
            if (!TextBindings.TryGetValue(key, out List<Control>? list))
            {
                list = new List<Control>();
                TextBindings[key] = list;
            }
            list.AddRange(controls);
        }

        /// <summary>动态文本：窗体标题不是控件，单独刷（见 <see cref="ILocalizable"/>）</summary>
        public void ApplyDynamicText()
        {
            Text = Locale.T("settings.title");
            // 底部那条估算含数字，不在 TextBindings 里；但它的措辞是本地化的，切语言时要跟着重刷
            UpdateMemoryEstimate();
        }

        private void Settings_Load(object sender, EventArgs e)
        {
            numListenPort.Value = ClampDecimal(_config.ListenPort, numListenPort.Minimum, numListenPort.Maximum);

            chkEnableCache.Checked = _config.EnableMemoryCache;
            trackEviction.Value = (int)(_config.EvictionThreshold * 100);
            trackStop.Value = (int)(_config.StopCachingThreshold * 100);

            LoadSsdSettings();

            // 开机与自动化（见 AGENTS.md §2.7）：开关 + 已选目标盘的摘要
            chkRunAtStartup.Checked = _config.RunAtStartup;
            chkKeepOfflineOnBoot.Checked = _config.KeepOfflineOnBoot;
            RefreshStartupDiskSummary();

            UpdateValueLabels();
            UpdateMemoryEstimate();
        }

        #region L2 缓存盘

        private void LoadSsdSettings()
        {
            chkEnableSsdCache.Checked = _config.EnableSsdCache;
            numSsdCacheGb.Value = ClampDecimal(_config.SsdCacheMaxBytes / (1024m * 1024 * 1024),
                numSsdCacheGb.Minimum, numSsdCacheGb.Maximum);

            // 保守线存的是占用率；这里只把滑块拨到配置值（真夹取在引擎侧，越界会落盘提醒）
            int conservative = (int)Math.Round(_config.SsdConservativeThreshold * 100);
            trackSsdConservative.Value = Math.Clamp(conservative,
                trackSsdConservative.Minimum, trackSsdConservative.Maximum);

            LoadSsdCacheDriveList();
        }

        /// <summary>
        /// 列出可选的 L2 缓存盘。
        ///
        /// **不再排除"被加速的那块盘"**：目标盘改成启动时才选，这里根本不知道会是哪一块。
        /// 那条"缓存目录不能与被加速盘同盘"的校验由引擎在**启动时**执行
        /// （见 <see cref="TargetService"/> 的启动校验 ⑥），所以这里不拦、也不该拦。
        /// </summary>
        private void LoadSsdCacheDriveList()
        {
            cmbSsdCacheDrive.Items.Clear();
            foreach (string root in DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            {
                cmbSsdCacheDrive.Items.Add(root);
            }

            // 配置里已选的缓存盘（可能已不存在）临时补进列表，保证能如实展示当前配置
            string? configured = string.IsNullOrWhiteSpace(_config.SsdCachePath)
                ? null
                : (Path.GetPathRoot(_config.SsdCachePath) ?? string.Empty).TrimEnd('\\');
            if (!string.IsNullOrEmpty(configured))
            {
                if (!cmbSsdCacheDrive.Items.Contains(configured))
                {
                    cmbSsdCacheDrive.Items.Add(configured);
                }
                cmbSsdCacheDrive.SelectedItem = configured;
            }
            if (cmbSsdCacheDrive.SelectedItem == null && cmbSsdCacheDrive.Items.Count > 0)
            {
                cmbSsdCacheDrive.SelectedIndex = 0;
            }
        }

        #endregion

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
            => value < min ? min : (value > max ? max : value);

        private void trackEviction_Scroll(object sender, EventArgs e)
        {
            if (trackEviction.Value >= trackStop.Value)
            {
                trackEviction.Value = trackStop.Value - 1;
            }
            UpdateValueLabels();
        }

        private void trackStop_Scroll(object sender, EventArgs e)
        {
            if (trackStop.Value <= trackEviction.Value)
            {
                trackStop.Value = trackEviction.Value + 1;
            }
            UpdateValueLabels();
        }

        private void trackSsdConservative_Scroll(object sender, EventArgs e) => UpdateValueLabels();

        private void numSsdCacheGb_ValueChanged(object sender, EventArgs e) => UpdateMemoryEstimate();

        private void chkCacheToggle_CheckedChanged(object sender, EventArgs e) => UpdateMemoryEstimate();

        private void UpdateValueLabels()
        {
            lblEvictionVal.Text = $"{trackEviction.Value}%";
            lblStopVal.Text = $"{trackStop.Value}%";
            lblSsdConservativeVal.Text = $"{trackSsdConservative.Value}%";

            // L1 的估算取决于「开始淘汰阈值」，所以这两个滑块一动就要重算
            UpdateMemoryEstimate();
        }

        /// <summary>
        /// 刷新窗口底部的「预计额外内存」提示。
        ///
        /// 口径（与引擎同源，见 Engine/SystemMemory.cs）：
        /// - **L2 索引**：槽位/索引结构常驻内存，约 <see cref="SsdCacheService.MetadataBytesPerBlock"/> 字节/块
        ///   （块 = <see cref="ServiceConstants.BlockSize"/>），所以"每 1 GiB 容量 ≈ 15 MiB 内存"；
        /// - **L1 索引**：L1 是动态池、没有固定上限，"预期能攒到的块数"按"**当前内存到开始淘汰水位的差距**"
        ///   （(开始淘汰阈值 − 当前系统内存使用率) × 物理内存）估；再按**与 L2 同一口径**只折算"缓存以外"的
        ///   管理内存（块数 × <see cref="CacheService.MetadataBytesPerBlock"/>），缓存数据本身不计入。
        ///   读不到系统内存时该项显示"—"，总量退化为"≥ L2 部分"（宁可少报，不多报）。
        /// </summary>
        private void UpdateMemoryEstimate()
        {
            long l2Bytes = chkEnableSsdCache.Checked
                ? (long)numSsdCacheGb.Value * 1024 * 1024 * 1024 / ServiceConstants.BlockSize
                    * SsdCacheService.MetadataBytesPerBlock
                : 0;

            long l1Bytes = 0;
            bool l1Known = true;
            if (chkEnableCache.Checked)
            {
                if (SystemMemory.TryRead(out MemoryStatus mem))
                {
                    long total = (long)mem.TotalPhysicalBytes;
                    long used = (long)(mem.MemoryLoadPercent / 100.0 * total);
                    long headroom = (long)(trackEviction.Value / 100.0 * total) - used;
                    // 口径同 L2：只算"缓存数据以外"的管理结构 ⇒ L1 预期块数 × 每块管理内存
                    if (headroom > 0)
                    {
                        l1Bytes = headroom / ServiceConstants.BlockSize * CacheService.MetadataBytesPerBlock;
                    }
                }
                else
                {
                    l1Known = false;
                }
            }

            string l1Text = l1Known ? FormatCapacity(l1Bytes) : "—";
            string totalText = l1Known ? FormatCapacity(l2Bytes + l1Bytes) : "≥ " + FormatCapacity(l2Bytes);
            lblMemEstimate.Text = string.Format(Locale.T("settings.memEstimate"),
                totalText, FormatCapacity(l2Bytes), l1Text);
        }

        /// <summary>容量按 1024 进制显示（MiB/GiB），与界面上的「容量预算(GiB)」同一口径</summary>
        private static string FormatCapacity(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
            {
                return string.Format("{0:0.##} GiB", bytes / 1024.0 / 1024 / 1024);
            }
            return string.Format("{0:0.#} MiB", bytes / 1024.0 / 1024);
        }

        /// <summary>
        /// 「L2 管理」：列出整机上找到的 L2 缓存，让用户挑一份做操作（见 <see cref="L2Manage"/>）。
        ///
        /// **与 L2 是否启用无关，永远可用**——它最常见的用途恰恰是"我已经关掉 L2 了，但想清掉以前留下的缓存"。
        /// </summary>
        private void btnL2Manage_Click(object sender, EventArgs e)
        {
            if (!L2Manage.Run(this, _config)) return;

            // 用户在操作窗口里把某一份设成了当前缓存盘：把下拉框与内存估算刷新过来。
            // **落盘仍由「保存」负责**（与其它设置项一致：取消就不会改到盘上的配置）。
            LoadSsdCacheDriveList();
            UpdateMemoryEstimate();
        }

        /// <summary>打开随程序落盘的帮助文档（不在就先补一份）。</summary>
        private void btnDocs_Click(object sender, EventArgs e)
        {
            if (HelpDocument.Open()) return;

            MessageBox.Show(string.Format(Locale.T("settings.msg.docsOpenFailed"), HelpDocument.FilePath),
                Locale.T("dialog.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            bool ssdEnabled = chkEnableSsdCache.Checked;
            string? ssdDrive = cmbSsdCacheDrive.SelectedItem as string;
            if (ssdEnabled && string.IsNullOrEmpty(ssdDrive))
            {
                MessageBox.Show(Locale.T("settings.msg.selectSsdDrive"), Locale.T("dialog.tip"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config.ListenPort = (int)numListenPort.Value;

            _config.EnableMemoryCache = chkEnableCache.Checked;
            _config.EvictionThreshold = trackEviction.Value / 100.0;
            _config.StopCachingThreshold = trackStop.Value / 100.0;

            _config.EnableSsdCache = ssdEnabled;
            _config.SsdCachePath = string.IsNullOrEmpty(ssdDrive)
                ? string.Empty
                : Path.Combine(ssdDrive + Path.DirectorySeparatorChar, ServiceConstants.SsdCacheDirectoryName);
            _config.SsdCacheMaxBytes = (long)numSsdCacheGb.Value * 1024 * 1024 * 1024;
            _config.SsdConservativeThreshold = trackSsdConservative.Value / 100.0;

            // 开机与自动化：勾了「保持脱机」却一块盘都没选 ⇒ 那条计划任务会空跑，先拦住
            if (chkKeepOfflineOnBoot.Checked && _config.StartupDiskIdentities.Count == 0)
            {
                MessageBox.Show(Locale.T("settings.msg.keepOfflineNoDisk"), Locale.T("dialog.tip"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config.RunAtStartup = chkRunAtStartup.Checked;
            _config.KeepOfflineOnBoot = chkKeepOfflineOnBoot.Checked;

            DialogResult = DialogResult.OK;
            Close();
        }

        #region 开机与自动化

        /// <summary>
        /// 「选择硬盘…」：**多选**本机物理盘（见 PickerForm 的多选构造），结果写入配置里的**开机目标盘**列表。
        /// 打开时按当前列表自动勾选，确定即覆盖、取消则什么都不改（与设置窗其余项一致：取消不落盘）。
        ///
        /// 这些盘会被两条开机流程用到：① 开机（含未登录）时确保脱机；② 登录后启动主界面。
        /// 它们**不是**"自动加速名单"——加速仍然由用户在主界面点「启动加速」时**当场选盘**
        /// （见 PickerForm 类注释的"选盘时机"），这里存的只是"开机要处理哪几块"。
        /// </summary>
        private void btnPickStartupDisks_Click(object? sender, EventArgs e)
        {
            List<PhysicalDiskInfo> disks;
            try
            {
                disks = PhysicalDiskHandle.Enumerate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Locale.T("settings.msg.enumerateFailed", ex.Message), Locale.T("dialog.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (disks.Count == 0)
            {
                MessageBox.Show(Locale.T("settings.msg.noDisks"), Locale.T("dialog.tip"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var picker = new PickerForm(disks, _config.StartupDiskIdentities,
                PhysicalDiskHandle.GetL2CacheDiskNumber(_config.EnableSsdCache, _config.SsdCachePath));
            if (picker.ShowDialog(this) != DialogResult.OK) return;

            _config.StartupDiskIdentities.Clear();
            foreach (PhysicalDiskInfo disk in picker.SelectedDisks)
            {
                _config.StartupDiskIdentities.Add(PhysicalDiskHandle.DescribeIdentity(disk));
            }
            RefreshStartupDiskSummary();
        }

        /// <summary>
        /// 「按盘符添加」：把输入里的盘符（`Y,Z` / `Y: Z:` / `Y:\` 都认）解析成物理盘并**追加**到开机目标盘列表。
        ///
        /// **为什么盘符只能用来"添加"、不能用来"保存"**：盘符属于**卷**、不是物理盘——一块盘可以多卷多盘符、
        /// 也可以（未格式化时）没有盘符；而且本程序对目标盘做的是**整盘脱机**，脱机后卷随之消失、盘符根本不存在。
        /// 开机时目标盘恰恰就是"已脱机"的状态，所以最终存下来的必须是**设备身份串**（型号|序列号），
        /// 盘符只在"此刻盘还联机"时充当解析入口（见 <see cref="DiskConfig.StartupDiskIdentities"/>）。
        /// </summary>
        private void btnAddByDrive_Click(object? sender, EventArgs e)
        {
            List<string> letters = ParseDriveLetters(txtDriveLetters.Text);
            if (letters.Count == 0)
            {
                MessageBox.Show(Locale.T("settings.msg.noDriveLetter"), Locale.T("dialog.tip"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<PhysicalDiskInfo> disks;
            try
            {
                disks = PhysicalDiskHandle.Enumerate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Locale.T("settings.msg.enumerateFailed", ex.Message), Locale.T("dialog.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var added = new List<string>();
            var problems = new List<string>();
            foreach (string letter in letters)
            {
                if (!PhysicalDiskHandle.TryGetDiskNumberOfVolume(letter, out int number))
                {
                    // 脱机盘没有盘符——这正是"用盘符找不到它"的根源，如实说明并指路「选择硬盘…」
                    problems.Add(Locale.T("settings.drive.notFound", letter));
                    continue;
                }

                PhysicalDiskInfo? info = disks.FirstOrDefault(d => d.DiskNumber == number);
                if (info == null)
                {
                    problems.Add(Locale.T("settings.drive.notFound", letter));
                    continue;
                }

                string blocked = PhysicalDiskHandle.DescribeTargetDiskBlockReason(info.DiskNumber);
                if (blocked.Length > 0)
                {
                    // 与选盘对话框 / 引擎启动校验同一条硬拦：这些盘做不了加速目标，也绝不能脱机
                    problems.Add(Locale.T("settings.drive.blocked", letter, blocked));
                    continue;
                }

                string identity = PhysicalDiskHandle.DescribeIdentity(info);
                if (_config.StartupDiskIdentities.Contains(identity)) continue;   // 已在列表里：跳过（幂等）

                _config.StartupDiskIdentities.Add(identity);
                added.Add(DescribeDiskForUi(info));
            }

            txtDriveLetters.Clear();
            RefreshStartupDiskSummary();

            if (problems.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, problems), Locale.T("dialog.tip"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (added.Count > 0)
            {
                MessageBox.Show(Locale.T("settings.msg.disksAdded", string.Join("；", added)),
                    Locale.T("dialog.tip"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>从自由文本里抽出盘符（只认字母，忽略冒号/反斜杠/逗号/空格），按出现顺序去重</summary>
        private static List<string> ParseDriveLetters(string text)
        {
            var letters = new List<string>();
            foreach (Match match in Regex.Matches(text ?? string.Empty, "[A-Za-z]"))
            {
                string letter = match.Value.ToUpperInvariant() + ":";
                if (!letters.Contains(letter)) letters.Add(letter);
            }
            return letters;
        }

        /// <summary>把已选目标盘刷成一行摘要（**当前盘符 + 型号**；盘找不到时如实标出）</summary>
        private void RefreshStartupDiskSummary()
        {
            if (_config.StartupDiskIdentities.Count == 0)
            {
                lblStartupDisks.Text = Locale.T("settings.startupNone");
                return;
            }

            var parts = new List<string>();
            try
            {
                List<PhysicalDiskInfo> disks = PhysicalDiskHandle.Enumerate();
                foreach (string identity in _config.StartupDiskIdentities)
                {
                    int number = PhysicalDiskHandle.ResolveDiskNumber(disks, identity);
                    PhysicalDiskInfo? info = number >= 0 ? disks.FirstOrDefault(d => d.DiskNumber == number) : null;
                    parts.Add(info != null ? DescribeDiskForUi(info) : Locale.T("settings.startupMissing", identity));
                }
            }
            catch
            {
                // 枚举失败（例如盘正在被独占）：退化成只列身份串，不能因为一句提示挂掉设置窗口
                parts.AddRange(_config.StartupDiskIdentities);
            }

            lblStartupDisks.Text = Locale.T("settings.startupSelected", string.Join("；", parts));
        }

        /// <summary>展示用：「Y:、Z:（型号）」；盘当前没有盘符（已脱机 / 未格式化）时用文案说明</summary>
        private static string DescribeDiskForUi(PhysicalDiskInfo info)
        {
            string drives = info.DriveLetters.Count > 0
                ? string.Join("、", info.DriveLetters)
                : Locale.T("settings.drive.none");
            string model = info.Model.Length > 0 ? info.Model : Locale.T("selectDisk.modelUnknown");
            return $"{drives}（{model}）";
        }

        /// <summary>勾选开关时把摘要刷一遍（只是如实显示当前选择，不做任何拦截）</summary>
        private void chkKeepOffline_CheckedChanged(object? sender, EventArgs e) => RefreshStartupDiskSummary();

        #endregion
    }
}
