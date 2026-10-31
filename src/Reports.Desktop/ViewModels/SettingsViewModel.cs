using System.Diagnostics;
using System.Text;
using System.IO;
using Reports.Core.Identity;
using Reports.Core.Identity.Exceptions;
using Reports.Core.Services;
using Reports.Integration;
using Reports.Desktop.Infrastructure;

namespace Reports.Desktop.ViewModels;

/// <summary>
/// Вкладка «Налаштування»: реквізити частини, джерело даних і шаблон.
/// Постійні частини документа після зміни треба перенести у шаблон —
/// для цього є команда «Перебудувати шаблон».
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly Action<string> _report;
    private string _connectionReport = string.Empty;

    public SettingsViewModel(AppServices services, Action<string> report)
    {
        _services = services;
        _report = report;

        SaveCommand = new RelayCommand(Save, () => CanChangeSettings);
        RebuildTemplateCommand = new RelayCommand(RebuildTemplate, () => _services.Guard.IsAllowed(AccessActions.RebuildTemplate));
        OpenTemplateCommand = new RelayCommand(() => OpenInShell(_services.TemplatePath));
        OpenDataFolderCommand = new RelayCommand(() => OpenInShell(_services.DataFolder));
        ForgetTokenCommand = new RelayCommand(ForgetToken, () => _services.Guard.IsAllowed(AccessActions.ForgetToken));
        CheckConnectionCommand = new AsyncRelayCommand(CheckConnectionAsync, () => !UseDemoData && CanChangeSettings);
    }

    private AppSettings S => _services.Settings;

    /// <summary>Налаштування змінює лише адміністратор; для оператора елементи вимкнені.</summary>
    public bool CanChangeSettings => _services.Guard.IsAllowed(AccessActions.ChangeSettings);

    /// <summary>Дозвіл вивантаження у Google-таблицю (тільки адміністратор).</summary>
    public bool UploadEnabled
    {
        get => S.UploadEnabled;
        set { S.UploadEnabled = value; OnPropertyChanged(); }
    }

    public string Classification
    {
        get => S.Classification;
        set { S.Classification = value; OnPropertyChanged(); }
    }

    public string Addressee
    {
        get => S.Addressee;
        set { S.Addressee = value; OnPropertyChanged(); }
    }

    public string Resolution
    {
        get => S.Resolution;
        set { S.Resolution = value; OnPropertyChanged(); }
    }

    public string ResolutionRank
    {
        get => S.ResolutionRank;
        set { S.ResolutionRank = value; OnPropertyChanged(); }
    }

    public string ResolutionName
    {
        get => S.ResolutionName;
        set { S.ResolutionName = value; OnPropertyChanged(); }
    }

    public string SubunitText
    {
        get => S.SubunitText;
        set { S.SubunitText = value; OnPropertyChanged(); }
    }

    public string OrderDefault
    {
        get => S.OrderDefault;
        set { S.OrderDefault = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Ідентифікатор таблиці. Якщо вставили адресу цілком — беремо з неї
    /// сам ідентифікатор, бо інакше Google відповість «не знайдено».
    /// </summary>
    public string SpreadsheetId
    {
        get => S.SpreadsheetId;
        set
        {
            S.SpreadsheetId = GoogleSheetsResponseSource.NormalizeSpreadsheetId(value);
            OnPropertyChanged();
        }
    }

    public string ResponsesSheet
    {
        get => S.ResponsesSheet;
        set { S.ResponsesSheet = value; OnPropertyChanged(); }
    }

    public string DirectoriesSheet
    {
        get => S.DirectoriesSheet;
        set { S.DirectoriesSheet = value; OnPropertyChanged(); }
    }

    public string PeopleSheet
    {
        get => S.PeopleSheet;
        set { S.PeopleSheet = value; OnPropertyChanged(); }
    }

    public string OutputFolder
    {
        get => S.OutputFolder;
        set { S.OutputFolder = value; OnPropertyChanged(); }
    }

    public bool UseDemoData
    {
        get => S.UseDemoData;
        set
        {
            S.UseDemoData = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceDescription));
        }
    }

    public string SourceDescription => UseDemoData
        ? "Джерело: демонстраційні дані (без інтернету)."
        : "Джерело: Google Таблиця.";

    public string CredentialsStatus => File.Exists(_services.CredentialsPath)
        ? "Файл credentials.json знайдено."
        : "credentials.json відсутній — Google-синхронізація не працюватиме.";

    public string TemplatePath => _services.TemplatePath;
    public string DataFolder => _services.DataFolder;

    public RelayCommand SaveCommand { get; }
    public RelayCommand RebuildTemplateCommand { get; }
    public RelayCommand OpenTemplateCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand ForgetTokenCommand { get; }
    public AsyncRelayCommand CheckConnectionCommand { get; }

    /// <summary>Результат перевірки зʼєднання з таблицею.</summary>
    public string ConnectionReport
    {
        get => _connectionReport;
        private set => Set(ref _connectionReport, value);
    }

    /// <summary>
    /// Відкриває таблицю, підбирає назви аркушів і звіряє заголовки колонок
    /// форми з полями моделі. Знайдені назви одразу записуються у налаштування,
    /// щоб не доводилося вгадувати їх вручну.
    /// </summary>
    private async Task CheckConnectionAsync()
    {
        ConnectionReport = "Перевірка…";

        try
        {
            var diagnosis = await _services.Connection.CheckAsync();

            var text = new StringBuilder();
            text.AppendLine("Аркуші у таблиці: " + string.Join(", ", diagnosis.SheetTitles));
            text.AppendLine();

            if (diagnosis.ResponsesSheet is null)
            {
                text.AppendLine("Аркуш із відповідями форми не знайдено.");
                text.AppendLine("Вкажіть його назву вручну у полі «Аркуш відповідей».");
                ConnectionReport = text.ToString();
                _report("Перевірка: аркуш відповідей не знайдено.");
                return;
            }

            // Підібрані назви одразу застосовуємо, щоб синхронізація запрацювала.
            ResponsesSheet = diagnosis.ResponsesSheet;
            if (diagnosis.DirectoriesSheet is not null) DirectoriesSheet = diagnosis.DirectoriesSheet;
            if (diagnosis.PeopleSheet is not null) PeopleSheet = diagnosis.PeopleSheet;

            text.AppendLine($"Відповіді : «{diagnosis.ResponsesSheet}», рядків — {diagnosis.ResponseRows}");
            text.AppendLine($"Довідники : {diagnosis.DirectoriesSheet ?? "не знайдено"}");
            text.AppendLine($"Люди      : {diagnosis.PeopleSheet ?? "не знайдено"}");
            text.AppendLine();

            text.AppendLine("Розпізнані колонки форми:");
            foreach (var field in diagnosis.MatchedFields)
                text.AppendLine("  • " + field);

            if (diagnosis.MissingFields.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("НЕ знайдено: " + string.Join(", ", diagnosis.MissingFields));
                text.AppendLine("Перейменуйте питання у формі так, щоб у заголовку було");
                text.AppendLine("ключове слово (БпЛА, ціль, кількість, населений пункт, боєприпас).");
            }
            else
            {
                text.AppendLine();
                text.AppendLine("Усі потрібні колонки знайдені. Можна синхронізувати.");
            }

            ConnectionReport = text.ToString();
            _services.SettingsService.Save();
            _report(diagnosis.IsUsable
                ? "Зʼєднання перевірено — таблиця придатна для синхронізації."
                : "Зʼєднання встановлено, але не всі колонки розпізнані.");
        }
        catch (AccessDeniedException ex)
        {
            ConnectionReport = string.Empty;
            _report(UiText.Describe(ex));
        }
        catch (GoogleAccessException ex)
        {
            ConnectionReport = ex.Message;
            _report("Google: не вдалося перевірити зʼєднання.");
        }
        catch (Exception ex)
        {
            ConnectionReport = "Помилка перевірки: " + ex.Message;
            _report("Помилка перевірки зʼєднання.");
        }
    }

    /// <summary>Ідентифікатор порожній (лише для демо-режиму) або схожий на справжній ID таблиці Google.</summary>
    private bool SpreadsheetIdIsAcceptable() =>
        string.IsNullOrEmpty(S.SpreadsheetId)
            ? UseDemoData
            : SheetInputValidator.IsValidSpreadsheetId(S.SpreadsheetId);

    private void Save()
    {
        if (!SpreadsheetIdIsAcceptable())
        {
            _report("Некоректний ідентифікатор таблиці: очікується 20–100 символів (літери, цифри, «-», «_»). Налаштування не збережено.");
            return;
        }

        try
        {
            _services.SettingsService.Save();
            OnPropertyChanged(nameof(CredentialsStatus));
            _report("Налаштування збережено.");
        }
        catch (AccessDeniedException ex)
        {
            _report(UiText.Describe(ex));
        }
    }

    private void RebuildTemplate()
    {
        if (!SpreadsheetIdIsAcceptable())
        {
            _report("Некоректний ідентифікатор таблиці: налаштування не збережено, шаблон не перебудовано.");
            return;
        }

        try
        {
            _services.SettingsService.Save();
            _services.RebuildTemplateAction.Execute();
            _report("Шаблон перебудовано за поточними налаштуваннями.");
        }
        catch (AccessDeniedException ex)
        {
            _report(UiText.Describe(ex));
        }
    }

    private void ForgetToken()
    {
        try
        {
            _services.ForgetTokenAction.Execute();
            _report("Збережений токен Google видалено — наступного разу буде запит згоди.");
        }
        catch (AccessDeniedException ex)
        {
            _report(UiText.Describe(ex));
        }
        catch (Exception ex)
        {
            _report("Не вдалося видалити токен: " + ex.Message);
        }
    }

    private void OpenInShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _report("Не вдалося відкрити: " + ex.Message);
        }
    }
}
