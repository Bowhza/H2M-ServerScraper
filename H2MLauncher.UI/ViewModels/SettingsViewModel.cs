﻿using System.IO;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core;
using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;

using Microsoft.Win32;

using Nogic.WritableOptions;

namespace H2MLauncher.UI.ViewModels;

public partial class SettingsViewModel : DialogViewModelBase
{
    [ObservableProperty]
    private string _mwrLocation = "";

    [ObservableProperty]
    private string _iw4mMasterServerUrl = "";

    [ObservableProperty]
    private bool _gameCommunicationEnabled = false;

    public ICommand ApplyCommand { get; set; }

    public ICommand CancelCommand { get; set; }

    public SettingsViewModel(IWritableOptions<H2MLauncherSettings> options)
    {
        // init properties from settings
        MwrLocation = options.CurrentValue.MWRLocation;
        Iw4mMasterServerUrl = options.CurrentValue.IW4MMasterServerUrl;
        GameCommunicationEnabled = options.CurrentValue.GameMemoryCommunication;

        ApplyCommand = new RelayCommand(() =>
        {
            // write back to settings
            options.Update((settings) => settings with
            {
                IW4MMasterServerUrl = Iw4mMasterServerUrl,
                MWRLocation = MwrLocation,
                GameMemoryCommunication = GameCommunicationEnabled,
            }, reload: true);

            CloseCommand.Execute(true);
        }, 
        () => CloseCommand.CanExecute(true));

        CancelCommand = CloseCommand;
    }

    [RelayCommand]
    private void SelectGameDirectory()
    {
        string directory = Path.GetDirectoryName(MwrLocation) ?? Environment.CurrentDirectory;
        string fileName = Path.GetFileName(MwrLocation) ?? "h2m-mod.exe";
        var dialog = new OpenFileDialog()
        {
            InitialDirectory = directory,
            CheckFileExists = true,
            FileName = fileName,
            Filter = "Executable (*.exe)|*.exe"
        };

        if (dialog.ShowDialog() == true)
        {
            MwrLocation = dialog.FileName;
        }
    }
}
 