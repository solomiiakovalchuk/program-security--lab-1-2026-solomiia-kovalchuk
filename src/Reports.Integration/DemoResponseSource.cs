using Reports.Core.Abstractions;
using Reports.Core.Domain;

namespace Reports.Integration;

/// <summary>
/// Демонстраційне джерело даних: вигадані подачі за останні три тижні.
/// Дозволяє відкрити програму й сформувати рапорт без Google і без інтернету —
/// саме на таких даних належить показувати роботу системи.
/// </summary>
public sealed class DemoResponseSource : IResponseSource
{
    private readonly DateTime _today;

    public DemoResponseSource(DateTime? today = null) => _today = (today ?? DateTime.Today).Date;

    public string Title => "Демонстраційні дані";

    public Task<RawDirectories> LoadDirectoriesAsync(CancellationToken ct = default) =>
        Task.FromResult(new RawDirectories
        {
            Uav = new[]
            {
                ("FPV", "FPV-дрон"),
                ("Кажан", "дрон «Кажан»"),
                ("Мавік", "БпЛА типу Mavic")
            },
            Targets = new[]
            {
                ("жива сила", "живої сили противника"),
                ("укріплення", "укріплення противника"),
                ("автомобіль", "автомобільної техніки противника"),
                ("розвід. БпЛА", "розвідувальних безпілотних літальних апаратів противника")
            },
            Munitions = new[]
            {
                ("ТШ-200", "тротилова шашка 200 г"),
                ("ВОГ-17", "ВОГ-17"),
                ("ЕД-8", "електродетонатор ЕД-8")
            },
            Settlements = new[]
            {
                ("Ліски", "Харківська"),
                ("Вербове", "Запорізька"),
                ("Ямпіль", "Донецька")
            },
            People = new[]
            {
                new Person
                {
                    Email = "commander@example.com",
                    FullName = "О. КОВАЛЕНКО",
                    Rank = "майор",
                    Position = "Командир зенітної батареї військової частини А0000"
                },
                new Person
                {
                    Email = "operator1@example.com",
                    FullName = "П. ШЕВЧЕНКО",
                    Rank = "сержант",
                    Position = "Оператор БпЛА"
                }
            }
        });

    public Task<IReadOnlyList<RawResponseRow>> LoadResponsesAsync(CancellationToken ct = default)
    {
        var rows = new List<RawResponseRow>();
        int index = 2;

        void Add(int daysAgo, string op, string place, string uav, string target,
                 int count, string[] munitions, string status = "")
        {
            var date = _today.AddDays(-daysAgo);
            rows.Add(new RawResponseRow
            {
                RowKey = $"demo|{index++}",
                SubmittedAt = date.AddHours(19).AddMinutes(index * 3 % 57),
                Date = date,
                OperatorEmail = op,
                SettlementName = place,
                UavTypeName = uav,
                TargetTypeName = target,
                UavCount = count,
                MunitionNames = munitions,
                StatusMarker = status
            });
        }

        Add(18, "operator1@example.com", "Ліски", "FPV", "жива сила", 4, new[] { "ТШ-200", "ВОГ-17" });
        Add(17, "operator1@example.com", "Ліски", "FPV", "укріплення", 2, new[] { "ТШ-200" });
        Add(15, "operator2@example.com", "Вербове", "Кажан", "автомобіль", 1, new[] { "ЕД-8" });
        Add(13, "operator1@example.com", "Ліски", "FPV", "жива сила", 5, new[] { "ВОГ-17" });
        Add(11, "operator2@example.com", "Ямпіль", "Мавік", "розвід. БпЛА", 2, new[] { "ТШ-200" });
        Add(9, "operator1@example.com", "Вербове", "FPV", "укріплення", 3, new[] { "ТШ-200", "ЕД-8" });
        Add(8, "operator2@example.com", "Ямпіль", "Кажан", "жива сила", 2, new[] { "ВОГ-17" });
        // Помилкова подача: не видалена, а скасована — слід залишається.
        Add(7, "operator2@example.com", "Ямпіль", "FPV", "жива сила", 40,
            new[] { "ВОГ-17" }, RecordStatusExtensions.CancelledMarker);
        Add(5, "operator1@example.com", "Ліски", "FPV", "автомобіль", 2, new[] { "ТШ-200" });
        Add(4, "operator1@example.com", "Ліски", "Кажан", "укріплення", 1, new[] { "ЕД-8", "ТШ-200" });
        Add(2, "operator2@example.com", "Вербове", "FPV", "жива сила", 6, new[] { "ВОГ-17", "ТШ-200" });
        Add(1, "operator1@example.com", "Ліски", "Мавік", "розвід. БпЛА", 1, Array.Empty<string>());
        Add(0, "operator1@example.com", "Ліски", "FPV", "жива сила", 3, new[] { "ТШ-200" });

        return Task.FromResult<IReadOnlyList<RawResponseRow>>(rows);
    }
}
