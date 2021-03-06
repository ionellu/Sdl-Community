﻿using System;
using System.IO;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using Sdl.Community.StarTransit.Shared.Models;
using Sdl.Community.StarTransit.Shared.Services;
using Sdl.Community.StarTransit.Shared.Utils;
using Sdl.Community.StarTransit.UI.Commands;
using Sdl.Community.StarTransit.UI.Controls;

namespace Sdl.Community.StarTransit.UI.ViewModels
{
	public class ReturnPackageMainWindowViewModel : BaseViewModel
	{
		private ICommand _createPackageCommand;
		private readonly ReturnFilesViewModel _returnFilesViewModel;
		private ReturnPackage _returnPackage;
		private readonly ReturnPackageService _returnService;
		private readonly CellViewModel _cellViewModel;
		private bool _active;
		private ReturnPackageMainWindow _window;

		public static readonly Log Log = Log.Instance;
		public Action CloseAction { get; set; }

		public ReturnPackageMainWindowViewModel(ReturnFilesViewModel returnFilesViewModel, CellViewModel cellViewModel, ReturnPackageMainWindow window)
		{
			_returnFilesViewModel = returnFilesViewModel;
			_cellViewModel = cellViewModel;
			_returnService = new ReturnPackageService();
			_window = window;
		}
		
		public ICommand CreatePackageCommand
		{
			get { return _createPackageCommand ?? (_createPackageCommand = new CommandHandler(CreatePackage, true)); }
		}

		public bool Active
		{
			get { return _active; }
			set
			{
				if (Equals(value, _active)) { return; }
				_active = value;
			}
		}

		private async void CreatePackage()
		{
			try
			{
				_returnPackage = _returnFilesViewModel.GetReturnPackage();
				if (_returnPackage.TargetFiles.Count == 0)
				{
					var dialog = new MetroDialogSettings
					{
						AffirmativeButtonText = "OK"

					};
					var result = await _window.ShowMessageAsync("No files selected!", "Please select at least one file.", MessageDialogStyle.Affirmative, dialog);
				}
				else
				{
					Active = true;
					string returnPackageFolderPath;

					if (_returnPackage.FolderLocation == null)
					{
						var projectPath = _returnPackage.ProjectLocation.Substring(0,
						_returnPackage.ProjectLocation.LastIndexOf(@"\", StringComparison.Ordinal));

						returnPackageFolderPath = CreateReturnPackageFolder(projectPath);
					}
					else
					{
						returnPackageFolderPath = CreateReturnPackageFolder(_returnPackage.FolderLocation);
					}

					//location of return package folder
					_returnPackage.FolderLocation = returnPackageFolderPath;

					await System.Threading.Tasks.Task.Run(() => _returnService.ExportFiles(_returnPackage));
					Active = false;
					_cellViewModel.ClearSelectedProjectsList();

					var dialog = new MetroDialogSettings
					{
						AffirmativeButtonText = "OK"

					};
					var result = await _window.ShowMessageAsync("Informative message", "The target file(s) was successfully returned", MessageDialogStyle.Affirmative, dialog);
					if (result == MessageDialogResult.Affirmative)
					{
						CloseAction();
					}
				}
			}
			catch (Exception ex)
			{
				Log.Logger.Error($"CreatePackage method: {ex.Message}\n {ex.StackTrace}");
			}
		}

		/// <summary>
		/// Create return  package folder in the studio project folder
		/// </summary>
		/// <param name="projectPath"></param>
		private string CreateReturnPackageFolder(string projectPath)
		{
			var returnPackageFolderPath = Path.Combine(projectPath, "Return package");
			if (!Directory.Exists(returnPackageFolderPath))
			{
				Directory.CreateDirectory(returnPackageFolderPath);
			}
			return returnPackageFolderPath;
		}
	}
}