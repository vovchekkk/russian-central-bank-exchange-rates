namespace Core.Services.Helpers;

public static class LinqExtensions
{
    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
        this IEnumerable<TOuter> outer,
        IEnumerable<TInner> inner,
        Func<TOuter, TKey> outerKeySelector,
        Func<TInner, TKey> innerKeySelector,
        Func<TOuter, TInner?, TResult> resultSelector)
    {
        return outer
            .GroupJoin(
                inner,
                outerKeySelector,
                innerKeySelector,
                (o, i) => new { o, i }
            )
            .SelectMany(
                x => x.i.DefaultIfEmpty(),
                (x, i) => resultSelector(x.o, i)
            );
    }
}