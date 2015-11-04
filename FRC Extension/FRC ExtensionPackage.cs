﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Net;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using RobotDotNet.FRC_Extension.MonoCode;
using RobotDotNet.FRC_Extension.WPILibFolder;
using VSLangProj;


namespace RobotDotNet.FRC_Extension
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidFRC_ExtensionPkgString)]
    //This attribute allows the extension to automatically update if it is a robot package.
    [ProvideAutoLoad("{f1536ef8-92ec-443c-9ed7-fdadf150da82}")]
    //This gives us an options page.
    [ProvideOptionPage(typeof(SettingsPageGrid), "FRC Options", "FRC Options", 0, 0, true)]
    public sealed class Frc_ExtensionPackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public Frc_ExtensionPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        //Store out local variables so we can control certain functions
        private OutputWriter m_writer;
        private bool m_deploying = false;
        OleMenuCommand m_deployMenuItem;
        OleMenuCommand m_debugMenuItem;
        OleMenuCommand m_downloadMonoMenuItem;
        OleMenuCommand m_installMonoMenuItem;

        MonoFile m_monoFile;

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();
            m_writer = OutputWriter.Instance;

            string monoFolder = WPILibFolderStructure.CreateMonoFolder();

            string monoFile = monoFolder + Path.DirectorySeparatorChar + DeployProperties.MonoVersion;

            m_monoFile = new MonoFile(monoFile);




            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                //Creating the deploy button. The BeforeQueryStatus event allows us to enable or disable the
                //button based on if we are a WPILib project or not.
                CommandID deployCommandID = new CommandID(GuidList.guidFRC_ExtensionCmdSet, (int)PkgCmdIDList.cmdidDeployCode);
                m_deployMenuItem = new OleMenuCommand((sender, e) => DeployCodeCallback(sender, e, false), deployCommandID);
                m_deployMenuItem.BeforeQueryStatus += QueryDeployButton;
                mcs.AddCommand(m_deployMenuItem);

                //Debug version of the deploy button
                CommandID debugCommandID = new CommandID(GuidList.guidFRC_ExtensionCmdSet, (int)PkgCmdIDList.cmdidDebugCode);
                m_debugMenuItem = new OleMenuCommand((sender, e) => DeployCodeCallback(sender, e, true), debugCommandID);
                m_debugMenuItem.BeforeQueryStatus += QueryDeployButton;
                mcs.AddCommand(m_debugMenuItem);

                //Download Mono Command Id
                CommandID downloadMonoCommandId = new CommandID(GuidList.guidFRC_ExtensionCmdSet, (int)PkgCmdIDList.cmdidDownloadMono);
                m_downloadMonoMenuItem = new OleMenuCommand(DownloadMonoCallback, downloadMonoCommandId);
                m_downloadMonoMenuItem.Visible = true;
                m_downloadMonoMenuItem.Enabled = true;
                mcs.AddCommand(m_downloadMonoMenuItem);

                CommandID installMonoCommandId = new CommandID(GuidList.guidFRC_ExtensionCmdSet, (int)PkgCmdIDList.cmdidInstallMono);
                m_installMonoMenuItem = new OleMenuCommand(InstallMonoCallback, installMonoCommandId);
                m_installMonoMenuItem.Visible = true;
                m_installMonoMenuItem.Enabled = true;
                mcs.AddCommand(m_installMonoMenuItem);



                //Adds a command so we can open NetConsole. 
                CommandID netconsoleCommandID = new CommandID(GuidList.guidFRC_ExtensionCmdSet,
                    (int) PkgCmdIDList.cmdidNetconsole);
                OleMenuCommand netconsoleItem = new OleMenuCommand(NetconsoleCallback, netconsoleCommandID);
                netconsoleItem.BeforeQueryStatus += QueryNetConsole;
                mcs.AddCommand(netconsoleItem);

                //For settings, we just want to pop up the standard settings menu.
                CommandID settingsCommandID = new CommandID(GuidList.guidFRC_ExtensionCmdSet,
                    (int)PkgCmdIDList.cmdidSettings);
                MenuCommand settingsItem = new MenuCommand(((sender, e) => OpenSettings()), settingsCommandID);
                mcs.AddCommand(settingsItem);

                //For settings, we just want to pop up the standard settings menu.
                CommandID aboutCommandID = new CommandID(GuidList.guidFRC_ExtensionCmdSet,
                    (int)PkgCmdIDList.cmdidAboutButton);
                MenuCommand aboutItem = new MenuCommand(((sender, e) => OpenAbout()), aboutCommandID);
                mcs.AddCommand(aboutItem);
            }
           

        }
        #endregion

        //This is called every time the menu is open, to check and see
        private void QueryDeployButton(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                var dte = GetService(typeof (DTE)) as DTE;
                var sb = (SolutionBuild2) dte.Solution.SolutionBuild;

                

                bool visable = false;
                if (sb.StartupProjects != null)
                {
                    string project = ((Array)sb.StartupProjects).Cast<string>().First();
                    Project startupProject = dte.Solution.Item(project);
                    var vsproject = startupProject.Object as VSLangProj.VSProject;
                    if (vsproject != null)
                    {
                        //If we are an assembly, and its named WPILib, enable the deploy
                        if ((from Reference reference in vsproject.References where reference.SourceProject == null select reference.Name).Any(name => name.Contains("WPILib")))
                        {
                            visable = true;
                        }
                    }
                }
                if (m_deploying)
                    visable = false;

                menuCommand.Visible = visable;
                if (menuCommand.Visible)
                {
                    bool enabled = ((Array)sb.StartupProjects).Cast<string>().Count() == 1;
                    
                    menuCommand.Enabled = enabled;
                }
            }
        }

        //Check to see if NetConsole exits. If so we can enable the open button.
        private void QueryNetConsole(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                bool visable = File.Exists(@"C:\Program Files\NetConsole for cRIO\NetConsole.exe") ||
                               File.Exists(@"C:\Program Files (x86)\NetConsole for cRIO\NetConsole.exe");

                

                menuCommand.Visible = visable;
                if (menuCommand.Visible)
                {
                    menuCommand.Enabled = true;
                }
            }
        }

        private class ProgressChecker : IProgress<int>
        {
            private IVsStatusbar statusBar;
            public ProgressChecker()
            {
                statusBar = (IVsStatusbar)ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar));

            }

            private uint cookie = 0;
            private string label = "Downloading Mono...";

            public void Report(int value)
            {
                statusBar.Progress(ref cookie, 1, label, (uint)value, 100);
            }

            public void Finish()
            {
                statusBar.Progress(ref cookie, 0, "", 0, 0);
            }
        }

        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private class TimeoutWebClient : WebClient
        {
            private int m_timeout;

            public TimeoutWebClient(int timeout)
            {
                this.m_timeout = timeout;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var result = base.GetWebRequest(address);
                result.Timeout = this.m_timeout;
                return result;
            }
        }

        private async void DownloadMonoCallback(object sender, EventArgs e)
        {
            //TODO: Check for Internet
            bool haveInternet = false;

            try
            {
                using (var client = new TimeoutWebClient(1000))
                {
                    using (var stream = client.OpenRead(DeployProperties.MonoUrl))
                    {
                        haveInternet =  true;
                        DownloadMonoPopup();
                    }
                }
            }
            catch
            {
                haveInternet = false;
            }

            if (!haveInternet) return;


            string monoFolder = WPILibFolderStructure.CreateMonoFolder();

            string monoFile = monoFolder + Path.DirectorySeparatorChar + DeployProperties.MonoVersion;

            m_monoFile.FileName = monoFile;

            bool downloadNew = !m_monoFile.CheckFileValid();

            if (downloadNew)
            {
                ProgressChecker checker = new ProgressChecker();
                await m_monoFile.DownloadMono(checker);
                checker.Finish();

                //Verify Download
                bool verified = m_monoFile.CheckFileValid();

                if (verified)
                {
                    // Show a Message Box to prove we were here
                    IVsUIShell uiShell = (IVsUIShell) GetService(typeof (SVsUIShell));
                    Guid clsid = Guid.Empty;
                    int result;
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                        0,
                        ref clsid,
                        "Mono Successfully Downloaded",
                        string.Format(CultureInfo.CurrentCulture, "", this.ToString()),
                        string.Empty,
                        0,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                        OLEMSGICON.OLEMSGICON_INFO,
                        0, // false
                        out result));
                }
                else
                {
                    // Show a Message Box to prove we were here
                    IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
                    Guid clsid = Guid.Empty;
                    int result;
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                        0,
                        ref clsid,
                        "Mono Download Failed. Please Try Again",
                        string.Format(CultureInfo.CurrentCulture, "", this.ToString()),
                        string.Empty,
                        0,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                        OLEMSGICON.OLEMSGICON_INFO,
                        0, // false
                        out result));
                }

            }
        }

        private async void InstallMonoCallback(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null)
            {
                return;
            }

            bool properFileExists = true;

            properFileExists = m_monoFile.CheckFileValid();

            if (properFileExists)
            {
                //We can deploy
                await DeployMono(menuCommand);

                //bool success = await m_monoFile.UnzipMonoFile();
            }
            else
            {
                //Ask to see if we want to load the file or download it
                string retVal = LoadMonoPopup();

                if (!string.IsNullOrEmpty(retVal))
                {
                    //Check for valid file.
                    properFileExists = m_monoFile.CheckFileValid();

                    if (properFileExists)
                    {
                        //We can deploy
                        await DeployMono(menuCommand);
                    }
                    else
                    {
                        InvalidMonoPopup();
                    }

                }
                else
                {
                    DownloadMonoPopup();
                }
            }
        }

        private bool m_installing = false;

        private async System.Threading.Tasks.Task DeployMono(OleMenuCommand menuCommand)
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    SettingsPageGrid page = (SettingsPageGrid) GetDialogPage(typeof (SettingsPageGrid));
                    string teamNumber = GetTeamNumber(page);

                    if (teamNumber == null) return;



                    //Disable Install Button
                    m_installing = true;
                    menuCommand.Visible = false;

                    DeployManager m = new DeployManager(GetService(typeof (DTE)) as DTE);
                    MonoDeploy deploy = new MonoDeploy(teamNumber, m, m_monoFile);

                    deploy.DeployMono();

                    m_installing = false;
                    menuCommand.Visible = true;


                });


            }
            catch (Exception ex)
            {
                m_writer.WriteLine(ex.ToString());
                m_installing = false;
                menuCommand.Visible = true;
            }
        }

        private string GetTeamNumber(SettingsPageGrid page)
        {
            //Get Team Number
            string teamNumber = page.TeamNumber.ToString();
            if (teamNumber == "0")
            {
                //If its 0, we pop up a window asking teams to set it.
                TeamNumberNotSetErrorPopup();
                return null;

            }
            return teamNumber;
        }


        /// <summary>
        /// The function is called when the deploy button is pressed.
        /// </summary>
        private async void DeployCodeCallback(object sender, EventArgs e, bool debug)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null)
            {
                return;
            }
            if (!m_deploying)
            {
                try
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        SettingsPageGrid page = (SettingsPageGrid)GetDialogPage(typeof(SettingsPageGrid));

                        string teamNumber = GetTeamNumber(page);

                        if (teamNumber == null) return;

                        //Disable the deploy button
                        m_deploying = true;
                        menuCommand.Visible = false;
                        DeployManager m = new DeployManager(GetService(typeof(DTE)) as DTE);
                        m.DeployCode(teamNumber, page, debug);
                        m_deploying = false;
                        menuCommand.Visible = true;
                    });
                }
                catch (Exception ex)
                {
                    m_writer.WriteLine(ex.ToString());
                    m_deploying = false;
                    menuCommand.Visible = true;
                }
                
            }
        }

        /// <summary>
        /// This function is called when the NetConsole button is pressed.
        /// </summary>
        private async void NetconsoleCallback(object sender, EventArgs e)
        {
            Action funcCall = DeployManager.StartNetConsole;
            await System.Threading.Tasks.Task.Run(funcCall);
            //new System.Threading.Thread(DeployManager.StartNetConsole).Start();
        }

        public void OpenSettings()
        {
            ShowOptionPage(typeof (SettingsPageGrid));
        }


        public void OpenAbout()
        {
            //TODO: Get version

            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       "FRC Extension",
                       string.Format(CultureInfo.CurrentCulture, "", this.ToString()),
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

        public void TeamNumberNotSetErrorPopup()
        {
            OutputWriter.Instance.WriteLine("Team Number Not Set");

            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;

            uiShell.ShowMessageBox(0, ref clsid, "Team Number Not Set",
                $"Please see your team number. Click OK will open up the settings menu.", string.Empty, 0,
                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, OLEMSGICON.OLEMSGICON_INFO, 0, out result);

            if (result == 1)
            {
                OpenSettings();
            }
        }

        public void DownloadMonoPopup()
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       "Please Download Mono. This can be done by clicking the \nDownload Mono button.",
                       string.Format(CultureInfo.CurrentCulture, "", this.ToString()),
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

        public void InvalidMonoPopup()
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       "Mono file is Invalid. Please try again.",
                       string.Format(CultureInfo.CurrentCulture, "", this.ToString()),
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

        public string LoadMonoPopup()
        {
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;

            uiShell.ShowMessageBox(0, ref clsid, "Mono File Not Found",
                $"Mono file not found. Would you like to load an existing file?", string.Empty, 0,
                OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, OLEMSGICON.OLEMSGICON_INFO, 0, out result);

            if (result == 6)
            {
                return MonoFile.SelectMonoFile();
            }
            return null;
        }
    }
    
}
