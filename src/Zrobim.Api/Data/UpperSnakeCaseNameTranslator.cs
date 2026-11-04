using System.Text;
using Npgsql;

namespace Zrobim.Api.Data;

/// <summary>
/// Maps C# enum members to the UPPER_SNAKE_CASE labels of the PostgreSQL enums:
/// <c>BookingStatus.AwaitingPayment</c> ⇄ <c>'AWAITING_PAYMENT'</c>.
/// </summary>
public sealed class UpperSnakeCaseNameTranslator : INpgsqlNameTranslator
{
    public string TranslateTypeName(string clrName) => ToSnakeCase(clrName).ToLowerInvariant();

    public string TranslateMemberName(string clrName) => ToSnakeCase(clrName).ToUpperInvariant();

    private static string ToSnakeCase(string name)
    {
        var result = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                result.Append('_');
            result.Append(c);
        }

        return result.ToString();
    }
}
