﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Sdl.Community.MTCloud.Languages.Provider.Model;
using Sdl.Community.MTCloud.Provider.Commands;
using Sdl.Community.MTCloud.Provider.Helpers;
using Sdl.Community.MTCloud.Provider.Service;
using Sdl.Core.Globalization;
using Application = System.Windows.Forms.Application;

namespace Sdl.Community.MTCloud.Provider.ViewModel
{
	public class MTCodesViewModel : BaseViewModel
	{
		private readonly PrintService _printService;
		private readonly Languages.Provider.Languages _languages;

		private ICommand _saveCommand;
		private ICommand _printCommand;
		private ICommand _resetToDefaultsCommand;

		private MTCloudLanguage _selectedMtCode;
		private List<MTCloudLanguage> _mtCodes;
		private string _message;
		private string _messageColor;
		private string _query;
		private bool _isWaiting;
		private string _itemsCountLabel;
		
		public MTCodesViewModel(Window owner, Languages.Provider.Languages languages)
		{
			Owner = owner;
			_languages = languages;
						
			MTCodes = new List<MTCloudLanguage>(GetAllLanguages(false));

			_printService = new PrintService();
		}

		public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(Save));
			
		public ICommand PrintCommand
			=> _printCommand ?? (_printCommand = new RelayCommand<DataGrid>(Print));

		public ICommand ResetToDefaultsCommand => _resetToDefaultsCommand
		                                                ?? (_resetToDefaultsCommand = new RelayCommand(ResetToDefaults));

		public Window Owner { get; }

		public List<MTCloudLanguage> MTCodes
		{
			get => _mtCodes;
			set
			{
				_mtCodes = value;

				OnPropertyChanged(nameof(MTCodes));
				ItemsCountLabel = string.Format(PluginResources.Total_Languages, _mtCodes.Count);
			}
		}

		public MTCloudLanguage SelectedMTCode
		{
			get => _selectedMtCode;
			set
			{
				_selectedMtCode = value;
				OnPropertyChanged(nameof(SelectedMTCode));
			}
		}

		public string Message
		{
			get => _message;
			set
			{
				_message = value;
				OnPropertyChanged(nameof(Message));
			}
		}

		public string MessageColor
		{
			get => _messageColor;
			set
			{
				_messageColor = value;
				OnPropertyChanged(nameof(MessageColor));
			}
		}

		public string Query
		{
			get => _query;
			set
			{
				if (_query == value)
				{
					return;
				}

				_query = value;				
				OnPropertyChanged(nameof(Query));

				SearchLanguages(_query);
			}
		}

		public bool IsWaiting
		{
			get => _isWaiting;
			set
			{
				_isWaiting = value;
				OnPropertyChanged(nameof(IsWaiting));
			}
		}

		public string ItemsCountLabel
		{
			get => _itemsCountLabel;
			set
			{
				if (_itemsCountLabel == value)
				{
					return;
				}

				_itemsCountLabel = value;
				OnPropertyChanged(nameof(ItemsCountLabel));
			}
		}		

		public void SearchLanguages(string query)
		{
			var collectionViewSource = CollectionViewSource.GetDefaultView(MTCodes);

			if (!string.IsNullOrEmpty(query))
			{
				collectionViewSource.Filter = language =>
				{
					var model = language as MTCloudLanguage;

					return (model != null && model.Language.ToLower().Contains(query.ToLower()))
						|| (model != null && model.TradosCode.ToLower().Contains(query.ToLower()))
						|| (model != null && model.Region.ToLower().Contains(query.ToLower()))
						|| (model != null && model.MTCode.ToLower().Contains(query.ToLower()))
						|| (model != null && model.MTCodeLocale.ToLower().Contains(query.ToLower()));
				};

				SelectedMTCode = collectionViewSource.CurrentItem as MTCloudLanguage;
			}
			else
			{
				collectionViewSource.Filter = null;
			}
		
			var filtered = collectionViewSource.Cast<MTCloudLanguage>().ToList();
			var filteredCount = filtered.Count;
			var totalCount = MTCodes.Count;
			ItemsCountLabel = filteredCount < totalCount 
				? string.Format(PluginResources.Total_And_Filtered_Languages, totalCount, filteredCount) 
				: string.Format(PluginResources.Total_Languages, totalCount);
		}
	
		public void Print(DataGrid dataGrid)
		{
			IsWaiting = true;

			var collectionViewSource = CollectionViewSource.GetDefaultView(MTCodes);
			var filtered = collectionViewSource.Cast<MTCloudLanguage>().ToList();
			var filteredCount = filtered.Count;
			var totalCount = MTCodes.Count;
		
			if (filteredCount < totalCount)
			{				
				var filteredFilePath = Path.Combine(Languages.Provider.Constants.MTCloudFolderPath, "FilteredMTLanguageCodes.xlsx");
				_languages.SaveLanguages(filtered, filteredFilePath);
				
				
				IsWaiting = false;
				_printService.PrintFile(filteredFilePath);
			}
			else
			{
				IsWaiting = false;
				_printService.PrintFile(Languages.Provider.Constants.MTLanguageCodesFilePath);
			}
		}
	
		private IEnumerable<MTCloudLanguage> GetAllLanguages(bool reset)
		{
			var mtCloudLanguages = _languages.GetLanguages(reset);
			if (AddStudioLanguages(mtCloudLanguages))
			{
				_languages.SaveLanguages(mtCloudLanguages);
			}

			return mtCloudLanguages;
		}

		private bool AddStudioLanguages(ICollection<MTCloudLanguage> mtCloudLanguages)
		{
			var updated = false;

			var studioLanguages = Language.GetAllLanguages();

			foreach (var studioLanguage in studioLanguages)
			{
				var mtCloudLanguage = mtCloudLanguages.FirstOrDefault(e => e.TradosCode.Equals(studioLanguage.CultureInfo.Name));
				if (mtCloudLanguage == null)
				{							
					var languageName = GetLanguageName(studioLanguage, out var region);

					updated = true;
					var language = new MTCloudLanguage
					{
						Index = mtCloudLanguages.Count,
						Language = languageName,
						Region = region,
						TradosCode = studioLanguage.CultureInfo.Name,
						MTCode = string.Empty,
						MTCodeLocale = string.Empty
					};

					mtCloudLanguages.Add(language);
				}
			}

			return updated;
		}

		private string GetLanguageName(Language language, out string region)
		{
			region = string.Empty;
			if (language == null)
			{
				return null;
			}

			var languageName = language.DisplayName;			
			if (!string.IsNullOrEmpty(languageName))
			{
				var regexSplit = new Regex(@"(?<language>[^\(]*)\((?<region>[^\)]*)", RegexOptions.IgnoreCase);
				if (languageName.Contains("(") && languageName.Contains(")"))
				{
					var match = regexSplit.Match(languageName);
					if (match.Success)
					{
						languageName = match.Groups["language"].Value.Trim();
						region = match.Groups["region"].Value.Trim();
					}
				}
			}

			return languageName;
		}

		private void ResetToDefaults(object obj)
		{			
			MTCodes = new List<MTCloudLanguage>(GetAllLanguages(true));

			MessageBox.Show(PluginResources.Message_Successfully_reset_to_defaults, 
				Application.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void Save(object obj)
		{			
			_languages.SaveLanguages(MTCodes.ToList());

			WindowCloser.SetDialogResult(Owner, true);
			Owner.Close();
		}
	}
}