namespace AssestBundleTools.Application.Common.Abstractions;

/// <summary>
/// 定义应用层查询处理器的统一入口。
/// </summary>
/// <typeparam name="TQuery">查询请求类型。</typeparam>
/// <typeparam name="TResult">查询执行结果类型。</typeparam>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
