using SecureFilesMvc.Web.Models.ThreatModel;

namespace SecureFilesMvc.Web.Services;

public class ThreatCatalogService : IThreatCatalogService
{
    // ---------- Границы доверия ----------
    private static readonly IReadOnlyList<TrustBoundary> _boundaries = new List<TrustBoundary>
    {
        new()
        {
            Id = "TB-01",
            Name = "Браузер → Web API (HTTP)",
            CrossingData = "HTTP-запросы: JSON, multipart/form-data",
            Controls = new[]
            {
                "HTTPS + HSTS",
                "Аутентификация по API-ключу (Authorization header)",
                "Валидация входных данных на сервере",
                "Rate limiting на уровне обратного прокси"
            }
        },
        new()
        {
            Id = "TB-02",
            Name = "Web API → Файловое хранилище",
            CrossingData = "Бинарные файлы",
            Controls = new[]
            {
                "Генерация собственного имени файла (GUID) — защита от path traversal",
                "Проверка MIME по сигнатуре, а не по расширению",
                "Хранение вне webroot",
                "Проверка размера"
            }
        },
        new()
        {
            Id = "TB-03",
            Name = "Web API → БД (в будущем)",
            CrossingData = "SQL-запросы",
            Controls = new[] { "Параметризованные запросы", "Минимальные права пользователя БД" }
        }
    };

    // ---------- Угрозы ----------
    private static readonly IReadOnlyList<Threat> _threats = new List<Threat>
    {
        new()
        {
            Id = "TH-001",
            Category = StrideCategory.Spoofing,
            Title = "Подмена клиента при загрузке файла",
            Description = "Злоумышленник отправляет файл без валидного API-ключа или с чужим ключом.",
            TrustBoundaryId = "TB-01",
            RiskScore = 7.5,
            Mitigations = new[] { "API-key auth", "Отказ 401 без тела", "Логирование попыток" },
            BacklogItemIds = new[] { "SB-001" }
        },
        new()
        {
            Id = "TH-002",
            Category = StrideCategory.Tampering,
            Title = "Path Traversal при скачивании",
            Description = "Клиент запрашивает ../../web.config, используя имя файла из пути.",
            TrustBoundaryId = "TB-02",
            RiskScore = 9.0,
            Mitigations = new[] { "Хранение по GUID", "Резолв через Path.GetFullPath + проверка префикса" },
            BacklogItemIds = new[] { "SB-002" }
        },
        new()
        {
            Id = "TH-003",
            Category = StrideCategory.InformationDisclosure,
            Title = "Утечка файлов через предсказуемые URL",
            Description = "Имена файлов предсказуемы, что позволяет перебирать чужие документы.",
            TrustBoundaryId = "TB-02",
            RiskScore = 6.5,
            Mitigations = new[] { "GUID-имена", "Авторизация на каждый файл", "Отсутствие листинга директорий" },
            BacklogItemIds = new[] { "SB-003" }
        },
        new()
        {
            Id = "TH-004",
            Category = StrideCategory.DenialOfService,
            Title = "DoS через большие/множественные загрузки",
            Description = "Отправка сотен гигабайт или тысяч параллельных запросов.",
            TrustBoundaryId = "TB-01",
            RiskScore = 7.0,
            Mitigations = new[] { "MaxRequestBodySize", "MultipartBodyLengthLimit", "Rate limiting" },
            BacklogItemIds = new[] { "SB-004" }
        },
        new()
        {
            Id = "TH-005",
            Category = StrideCategory.ElevationOfPrivilege,
            Title = "Загрузка исполняемых файлов (.exe, .js)",
            Description = "Пользователь загружает скрипт, который потом исполняется на сервере/в браузере.",
            TrustBoundaryId = "TB-02",
            RiskScore = 8.5,
            Mitigations = new[] { "Whitelist расширений", "Проверка magic bytes", "Content-Disposition: attachment" },
            BacklogItemIds = new[] { "SB-005" }
        },
        new()
        {
            Id = "TH-006",
            Category = StrideCategory.Repudiation,
            Title = "Отсутствие следов загрузки/скачивания",
            Description = "Нельзя доказать, кто и когда загрузил файл.",
            TrustBoundaryId = "TB-01",
            RiskScore = 4.0,
            Mitigations = new[] { "Structured logging с correlation-id", "Логирование API-ключа (хеш)" },
            BacklogItemIds = new[] { "SB-006" }
        }
    };

    // ---------- Security Backlog ----------
    private static readonly List<SecurityBacklogItem> _backlog = new()
    {
        new() { Id = "SB-001", ThreatId = "TH-001", Title = "Внедрить ApiKeyAuthenticationHandler",
                Priority = BacklogPriority.Critical, Status = BacklogStatus.Done,
                MitigationSummary = "Реализован кастомный handler в Security/ApiKeyAuthenticationHandler.cs",
                Owner = "backend" },

        new() { Id = "SB-002", ThreatId = "TH-002", Title = "Защита от path traversal в FileStorageService",
                Priority = BacklogPriority.Critical, Status = BacklogStatus.Done,
                MitigationSummary = "Имена генерируются как GUID, путь валидируется через Path.GetFullPath",
                Owner = "backend" },

        new() { Id = "SB-003", ThreatId = "TH-003", Title = "Авторизация на каждый файл",
                Priority = BacklogPriority.High, Status = BacklogStatus.InProgress,
                MitigationSummary = "Планируется добавить ownership-модель (ownerId у файла)",
                Owner = "backend" },

        new() { Id = "SB-004", ThreatId = "TH-004", Title = "Настроить лимиты размера и rate limiting",
                Priority = BacklogPriority.High, Status = BacklogStatus.Done,
                MitigationSummary = "Установлены MaxRequestBodySize=10MB и MultipartBodyLengthLimit",
                Owner = "devops" },

        new() { Id = "SB-005", ThreatId = "TH-005", Title = "Whitelist MIME + magic bytes",
                Priority = BacklogPriority.Critical, Status = BacklogStatus.Done,
                MitigationSummary = "FileValidationService проверяет расширение и первые байты",
                Owner = "backend" },

        new() { Id = "SB-006", ThreatId = "TH-006", Title = "Structured logging с correlation-id",
                Priority = BacklogPriority.Medium, Status = BacklogStatus.Open,
                MitigationSummary = "Планируется Serilog + RequestIdMiddleware",
                Owner = "backend" }
    };

    public IReadOnlyList<TrustBoundary> GetTrustBoundaries() => _boundaries;
    public IReadOnlyList<Threat> GetThreats() => _threats;
    public IReadOnlyList<SecurityBacklogItem> GetBacklog() => _backlog;
}
