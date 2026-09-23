// Shop.Infrastructure/Persistence/Configurations/Json/JsonModelConfigurator.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using WebApplication3.Infrastructure;

namespace WebApplication3.Infrastructure.Persistence.Configurations.Json;

public static class JsonModelConfigurator
{
    public static void ApplyFromJson(ModelBuilder modelBuilder, string jsonPath, Assembly domainAssembly)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Entity config not found: {jsonPath}");

        var json = File.ReadAllText(jsonPath);
        var root = JsonSerializer.Deserialize<EntityConfigRoot>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid entity-config.json");

        foreach (var entityCfg in root.Entities)
        {
            var clrType = domainAssembly.GetType($"Shop.Domain.{entityCfg.Name}")
                          ?? domainAssembly.GetTypes().FirstOrDefault(t => t.Name == entityCfg.Name)
                          ?? throw new InvalidOperationException($"Type '{entityCfg.Name}' not found in {domainAssembly.FullName}");

            var builder = modelBuilder.Entity(clrType);

            // Таблица
            if (!string.IsNullOrWhiteSpace(entityCfg.Table))
                builder.ToTable(entityCfg.Table);

            // Свойства
            foreach (var (propName, propCfg) in entityCfg.Properties)
            {
                var prop = builder.Property(propName);
                ApplyPropertyConfig(prop, propCfg);

                if (propCfg.IsKey)
                    builder.HasKey(propName);

                if (propCfg.Unique)
                    builder.HasIndex(propName).IsUnique();
                else if (propCfg.Index)
                    builder.HasIndex(propName);
            }

            // Owned-коллекции
            foreach (var ownedCfg in entityCfg.Owned)
            {
                ApplyOwnedConfig(builder, ownedCfg);
            }
        }
    }

    private static void ApplyPropertyConfig<T>(PropertyBuilder<T> prop, PropertyConfig cfg)
    {
        if (cfg.Required) prop.IsRequired();
        if (cfg.MaxLength.HasValue) prop.HasMaxLength(cfg.MaxLength.Value);
        if (!string.IsNullOrWhiteSpace(cfg.ColumnType)) prop.HasColumnType(cfg.ColumnType);
    }

    private static void ApplyOwnedConfig<T>(EntityTypeBuilder<T> builder, OwnedConfig cfg) where T : class
    {
        // Настраиваем навигацию на использование backing field
        var nav = builder.Metadata.FindNavigation(cfg.Navigation);
        if (nav is not null && !string.IsNullOrWhiteSpace(cfg.BackingField))
            nav.SetPropertyAccessMode(PropertyAccessMode.Field);

        // OwnsMany<TOwned> через рефлексию
        var navigationType = builder.Metadata
            .FindNavigation(cfg.Navigation)!
            .ClrType
            .GetGenericArguments()
            .First();

        var ownsMany = typeof(EntityTypeBuilder<T>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "OwnsMany" && m.GetParameters().Length == 2)
            .MakeGenericMethod(navigationType);

        var actionType = typeof(Action<>).MakeGenericType(
            typeof(OwnedNavigationBuilder<,>).MakeGenericType(typeof(T), navigationType));

        var method = typeof(JsonModelConfigurator)
            .GetMethod(nameof(ConfigureOwnedGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(T), navigationType);

        var action = Delegate.CreateDelegate(actionType, method);

        ownsMany.Invoke(builder, new object[] { cfg.Navigation, action });
    }
    private static void ConfigureOwnedGeneric<TOwner, TOwned>(
    OwnedNavigationBuilder<TOwner, TOwned> owned)
    where TOwner : class
    where TOwned : class
    {
        // Здесь можно было бы прочитать properties из cfg, но для учебного примера
        // оставим настройку из JSON «на минималках» — см. Вариант 3.
        owned.WithOwner().HasForeignKey("OrderId");
    }
}