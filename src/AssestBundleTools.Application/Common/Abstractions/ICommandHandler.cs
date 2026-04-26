namespace AssestBundleTools.Application.Common.Abstractions;

/// <summary>
/// 定义应用层命令处理器的统一入口。
/// </summary>
/// <typeparam name="TCommand">命令请求类型。</typeparam>
/// <typeparam name="TResult">命令执行结果类型。</typeparam>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
