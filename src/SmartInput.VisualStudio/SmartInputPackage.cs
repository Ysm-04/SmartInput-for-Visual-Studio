using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SmartInput.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Smart Input", "Local input-state switching for Visual Studio", "0.2.0")]
    [ProvideOptionPage(typeof(SmartInputOptionsPage), "Smart Input", "常规", 0, 0, true)]
    [ProvideMenuResource("SmartInputCommands.CTMENU", 1)]
    [Guid(PackageGuidString)]
    public sealed class SmartInputPackage : AsyncPackage
    {
        public const string PackageGuidString = "A11D90FC-8F3D-493A-A227-D7A59C26EAE8";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            SessionSettings.Initialize(this);

            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null) return;

            var commandId = new CommandID(SmartInputCommandIds.CommandSet, SmartInputCommandIds.TogglePause);
            var command = new OleMenuCommand(TogglePause, commandId);
            command.BeforeQueryStatus += UpdateTogglePauseCommand;
            commandService.AddCommand(command);
        }

        private static void TogglePause(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SessionSettings.TogglePaused();
        }

        private static void UpdateTogglePauseCommand(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var command = sender as OleMenuCommand;
            if (command == null) return;

            var settings = SessionSettings.Current;
            command.Enabled = settings.AutomaticSwitchingEnabled;
            command.Text = settings.Paused ? "Smart Input: 恢复自动切换" : "Smart Input: 暂停自动切换";
        }
    }
}
