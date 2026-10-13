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

namespace FlyDisk
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnStartStop = new Button();
            txtLog = new TextBox();
            btnClearLog = new Button();
            statusStrip1 = new StatusStrip();
            lblTargetStatus = new ToolStripStatusLabel();
            menuStrip1 = new MenuStrip();
            设置ToolStripMenuItem = new ToolStripMenuItem();
            检查器ToolStripMenuItem = new ToolStripMenuItem();
            labelTitle = new Label();
            labelSubtitle = new Label();
            statusStrip1.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // btnStartStop
            // 
            // 窗口可以自由拖动大小（见 Form1.LayoutDynamic）：宽度跟着窗口变、纵向位置由顶部定住
            btnStartStop.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnStartStop.Location = new Point(12, 141);
            btnStartStop.Name = "btnStartStop";
            btnStartStop.Size = new Size(560, 50);
            btnStartStop.TabIndex = 1;
            btnStartStop.Text = "启动";
            btnStartStop.UseVisualStyleBackColor = true;
            btnStartStop.Click += btnStartStop_Click;
            // 
            // txtLog
            // 
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtLog.BackColor = Color.White;
            txtLog.ForeColor = Color.Black;
            txtLog.Location = new Point(12, 197);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            // 高度是**算出来的**（见 Form1.LayoutDynamic）：从顶部一直撑到"清空日志按钮行 + 状态栏"之上，
            // 于是日志框底部贴近软件底端；这里只是设计期的初值，运行时每次尺寸变化都会重算。
            txtLog.Size = new Size(560, 195);
            txtLog.TabIndex = 2;
            // 
            // btnClearLog
            // 
            // 贴在日志框右下角、状态栏之上（位置与大小见 Form1.LayoutDynamic）
            btnClearLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClearLog.Location = new Point(488, 398);
            btnClearLog.Name = "btnClearLog";
            btnClearLog.Size = new Size(84, 26);
            btnClearLog.TabIndex = 3;
            btnClearLog.Text = "清空日志";
            btnClearLog.UseVisualStyleBackColor = true;
            btnClearLog.Click += btnClearLog_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { lblTargetStatus });
            statusStrip1.Location = new Point(0, 350);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(584, 22);
            statusStrip1.TabIndex = 3;
            statusStrip1.Text = "statusStrip1";
            // 
            // lblTargetStatus
            // 
            lblTargetStatus.Name = "lblTargetStatus";
            lblTargetStatus.Size = new Size(77, 17);
            lblTargetStatus.Text = "iSCSI 未监听";
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { 设置ToolStripMenuItem, 检查器ToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(584, 25);
            menuStrip1.TabIndex = 4;
            menuStrip1.Text = "menuStrip1";
            // 
            // 设置ToolStripMenuItem
            // 
            设置ToolStripMenuItem.Name = "设置ToolStripMenuItem";
            设置ToolStripMenuItem.Size = new Size(44, 21);
            设置ToolStripMenuItem.Text = "设置";
            // 
            // 检查器ToolStripMenuItem
            // 
            检查器ToolStripMenuItem.Name = "检查器ToolStripMenuItem";
            检查器ToolStripMenuItem.Size = new Size(56, 21);
            检查器ToolStripMenuItem.Text = "检查器";
            // 
            // labelTitle
            // 
            // 标题：宽度跟着窗口变（文字居中），窗口拉宽时标题仍在正中
            labelTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelTitle.Font = new Font("Microsoft YaHei UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 134);
            labelTitle.Location = new Point(0, 40);
            labelTitle.Name = "labelTitle";
            labelTitle.Size = new Size(584, 52);
            labelTitle.TabIndex = 5;
            labelTitle.Text = "FlyDisk";
            labelTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelSubtitle
            // 
            labelSubtitle.AutoSize = true;
            labelSubtitle.ForeColor = Color.Gray;
            // 位置由 Form1.LayoutDynamic/PlaceSubtitle 按当前窗口宽度算（左边缘贴在标题的水平中线上）
            labelSubtitle.Location = new Point(292, 92);
            labelSubtitle.Name = "labelSubtitle";
            labelSubtitle.Size = new Size(146, 17);
            labelSubtitle.TabIndex = 6;
            labelSubtitle.Text = "—— 机械硬盘块级加速器";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            // 默认比原来更高：多出来的高度全部给日志框（见 Form1.LayoutDynamic），其底部贴近软件底端
            ClientSize = new Size(584, 470);
            Controls.Add(labelSubtitle);
            Controls.Add(labelTitle);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            Controls.Add(txtLog);
            Controls.Add(btnClearLog);
            Controls.Add(btnStartStop);
            ForeColor = SystemColors.ControlText;
            // Sizable：允许用户拖动改变窗口大小（布局由 Form1.LayoutDynamic 自适应）
            FormBorderStyle = FormBorderStyle.Sizable;
            MainMenuStrip = menuStrip1;
            MaximizeBox = true;
            // 下限要保证标题、主按钮、日志框、清空按钮与状态栏都还放得下（含边框，故比 ClientSize 大一圈）
            MinimumSize = new Size(520, 460);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "FlyDisk - 机械硬盘加速器";
            Load += Form1_Load;
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button btnStartStop;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnClearLog;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblTargetStatus;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem 设置ToolStripMenuItem;
        private ToolStripMenuItem 检查器ToolStripMenuItem;
        private Label labelTitle;
        private Label labelSubtitle;
    }
}
