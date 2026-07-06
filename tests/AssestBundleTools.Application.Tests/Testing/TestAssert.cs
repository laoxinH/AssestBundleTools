namespace AssestBundleTools.Application.Tests.Testing;

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

    public static void Empty<T>(IReadOnlyCollection<T> items, string message)
    {
        if (items.Count != 0)
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

    public static T NotNull<T>(T? value, string message)
        where T : class
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }

        return value;
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}. Actual: {actual}.");
        }
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Expected exception of type {typeof(TException).Name} was not thrown.");
    }
}
