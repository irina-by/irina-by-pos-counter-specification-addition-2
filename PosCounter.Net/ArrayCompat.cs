namespace PosCounter.Net
{
    /// <summary>Пустые массивы для net452 — <see cref="System.Array.Empty{T}"/> доступен только с .NET 4.6.</summary>
    internal static class ArrayCompat
    {
        public static T[] Empty<T>()
        {
            return new T[0];
        }
    }
}
