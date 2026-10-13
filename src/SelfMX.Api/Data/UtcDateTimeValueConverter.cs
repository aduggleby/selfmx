using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SelfMX.Api.Data;

/// <summary>
/// All timestamps are stored as UTC. Marking them as such on the way in and out keeps PostgreSQL
/// (which rejects non-UTC values for timestamptz) working for values from query strings and
/// imports, and gives SQL Server reads (which come back Unspecified) the right Kind.
/// EF applies the converter to query parameters compared with these columns as well.
/// </summary>
public sealed class UtcDateTimeValueConverter() : ValueConverter<DateTime, DateTime>(
    v => AsUtc(v),
    v => AsUtc(v))
{
    // Unspecified values are UTC by convention; Local values (e.g. JSON with an offset) are converted
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static void Apply(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeValueConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeValueConverter>();
    }
}
