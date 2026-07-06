namespace AssestBundleTools.Infrastructure.Tests.Testing;

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        True(!condition, message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}. Actual: {actual}.");
        }
    }

    public static T NotNull<T>(T? value, string message)
        where T : class
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }

        return value;
    }

    public static void Null(object? value, string message)
    {
        if (value is not null)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static T Single<T>(IReadOnlyList<T> items)
    {
        if (items.Count != 1)
        {
            throw new InvalidOperationException($"Expected exactly one item but found {items.Count}.");
        }

        return items[0];
    }

    public static T Single<T>(IEnumerable<T> items)
    {
        var materialized = items.ToArray();
        return Single(materialized);
    }
}
