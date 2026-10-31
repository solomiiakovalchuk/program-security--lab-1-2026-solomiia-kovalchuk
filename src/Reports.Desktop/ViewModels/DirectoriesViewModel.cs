using System.Collections.ObjectModel;
using Reports.Core.Abstractions;
using Reports.Core.Domain;
using Reports.Core.Identity;
using Reports.Desktop.Infrastructure;

namespace Reports.Desktop.ViewModels;

/// <summary>
/// Рядок довідника для таблиці. Ліва колонка — те, що бачить оператор
/// у формі; права — те, що йде в текст рапорту (для місць — область).
/// </summary>
public sealed class ReferenceRow : ObservableObject
{
    public ReferenceRow(ReferenceItem item) => Item = item;

    public ReferenceItem Item { get; }

    public string Left
    {
        get => Item.ShortName;
        set { Item.ShortName = value; OnPropertyChanged(); }
    }

    public string Right
    {
        get => Item is Settlement s ? s.Region : Item.FullName;
        set
        {
            if (Item is Settlement s) s.Region = value;
            else Item.FullName = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>Один блок довідника з власними командами додавання, збереження й видалення.</summary>
public sealed class DirectorySection : ObservableObject
{
    private readonly IReferenceRepository _repository;
    private readonly Func<Task<IReadOnlyList<ReferenceItem>>> _load;
    private readonly Func<ReferenceItem> _create;
    private readonly Action<string> _report;

    private ReferenceRow? _selected;

    public DirectorySection(
        string title,
        string leftHeader,
        string rightHeader,
        string hint,
        IReferenceRepository repository,
        Func<Task<IReadOnlyList<ReferenceItem>>> load,
        Func<ReferenceItem> create,
        Action<string> report,
        Func<bool>? canEdit = null)
    {
        Title = title;
        LeftHeader = leftHeader;
        RightHeader = rightHeader;
        Hint = hint;
        _repository = repository;
        _load = load;
        _create = create;
        _report = report;

        var allowed = canEdit ?? (() => true);
        AddCommand = new RelayCommand(Add, allowed);
        SaveCommand = new AsyncRelayCommand(SaveAsync, allowed);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => allowed() && Selected is not null);
    }

    public string Title { get; }
    public string LeftHeader { get; }
    public string RightHeader { get; }
    public string Hint { get; }

    public ObservableCollection<ReferenceRow> Rows { get; } = new();

    public ReferenceRow? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public RelayCommand AddCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }

    public async Task LoadAsync()
    {
        Rows.Clear();
        foreach (var item in await _load())
            Rows.Add(new ReferenceRow(item));
    }

    private void Add()
    {
        var row = new ReferenceRow(_create()) { Left = "нова назва" };
        Rows.Add(row);
        Selected = row;
    }

    private async Task SaveAsync()
    {
        int saved = 0;
        foreach (var row in Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Left)) continue;
            await SaveItemAsync(row.Item);
            saved++;
        }

        await LoadAsync();
        _report($"{Title}: збережено записів — {saved}.");
    }

    private async Task DeleteAsync()
    {
        if (Selected is null) return;

        if (Selected.Item.Id != 0)
            await DeleteItemAsync(Selected.Item);

        Rows.Remove(Selected);
        Selected = null;
        _report($"{Title}: запис видалено.");
    }

    // Репозиторій узагальнений за типом елемента, тому диспетчеризуємо явно.
    private Task SaveItemAsync(ReferenceItem item) => item switch
    {
        UavType u => _repository.SaveAsync(u),
        TargetType t => _repository.SaveAsync(t),
        MunitionType m => _repository.SaveAsync(m),
        Settlement s => _repository.SaveAsync(s),
        _ => Task.CompletedTask
    };

    private Task DeleteItemAsync(ReferenceItem item) => item switch
    {
        UavType u => _repository.DeleteAsync(u),
        TargetType t => _repository.DeleteAsync(t),
        MunitionType m => _repository.DeleteAsync(m),
        Settlement s => _repository.DeleteAsync(s),
        _ => Task.CompletedTask
    };
}

/// <summary>Вкладка «Довідники»: чотири блоки + окремий список людей.</summary>
public sealed class DirectoriesViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly Action<string> _report;
    private Person? _selectedPerson;

    public DirectoriesViewModel(AppServices services, Action<string> report)
    {
        _services = services;
        _report = report;

        Sections = new ObservableCollection<DirectorySection>
        {
            new("БпЛА", "Коротка назва", "Повна назва",
                "Ліворуч — як у формі, праворуч — як у тексті рапорту.",
                services.References,
                async () => (await services.References.GetUavTypesAsync()).Cast<ReferenceItem>().ToList(),
                () => new UavType(), report, CanEdit),

            new("Цілі", "Тип цілі", "Повна назва (родовий відмінок)",
                "Права колонка підставляється у фразу «по знищенню …», тому родовий відмінок.",
                services.References,
                async () => (await services.References.GetTargetTypesAsync()).Cast<ReferenceItem>().ToList(),
                () => new TargetType(), report, CanEdit),

            new("Боєприпаси", "Коротка назва", "Повна назва",
                "Ліворуч — як у формі, праворуч — як у тексті рапорту.",
                services.References,
                async () => (await services.References.GetMunitionTypesAsync()).Cast<ReferenceItem>().ToList(),
                () => new MunitionType(), report, CanEdit),

            new("Місця", "Населений пункт", "Область",
                "Область підставляється у рапорт автоматично за назвою пункту.",
                services.References,
                async () => (await services.References.GetSettlementsAsync()).Cast<ReferenceItem>().ToList(),
                () => new Settlement(), report, CanEdit)
        };

        AddPersonCommand = new RelayCommand(AddPerson, CanEditPeople);
        SavePeopleCommand = new AsyncRelayCommand(SavePeopleAsync, CanEditPeople);
        DeletePersonCommand = new AsyncRelayCommand(DeletePersonAsync, () => CanEditPeople() && SelectedPerson is not null);
    }

    // Лише адміністратор редагує довідники й людей (реальна перевірка — у службах).
    private bool CanEdit() => _services.Guard.IsAllowed(AccessActions.EditReferenceData);

    private bool CanEditPeople() => _services.Guard.IsAllowed(AccessActions.EditPeople);

    public ObservableCollection<DirectorySection> Sections { get; }
    public ObservableCollection<Person> People { get; } = new();

    public Person? SelectedPerson
    {
        get => _selectedPerson;
        set => Set(ref _selectedPerson, value);
    }

    public RelayCommand AddPersonCommand { get; }
    public AsyncRelayCommand SavePeopleCommand { get; }
    public AsyncRelayCommand DeletePersonCommand { get; }

    public event EventHandler? DataChanged;

    public async Task LoadAsync()
    {
        foreach (var section in Sections)
            await section.LoadAsync();

        People.Clear();
        foreach (var person in await _services.People.GetAllAsync())
            People.Add(person);
    }

    private void AddPerson()
    {
        var person = new Person { Email = "new@example.com", FullName = "Нова особа" };
        People.Add(person);
        SelectedPerson = person;
    }

    private async Task SavePeopleAsync()
    {
        int saved = 0;
        foreach (var person in People)
        {
            if (string.IsNullOrWhiteSpace(person.Email)) continue;
            await _services.People.SaveAsync(person);
            saved++;
        }

        await LoadAsync();
        _report($"Люди: збережено записів — {saved}.");
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task DeletePersonAsync()
    {
        if (SelectedPerson is null) return;

        if (SelectedPerson.Id != 0)
            await _services.People.DeleteAsync(SelectedPerson);

        People.Remove(SelectedPerson);
        SelectedPerson = null;
        _report("Людину видалено з довідника.");
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
