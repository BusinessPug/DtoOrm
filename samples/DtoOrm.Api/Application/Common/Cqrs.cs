namespace DtoOrm.Api.Application.Common;

public interface IQuery<TResult> { }
public interface ICommand<TResult> { }

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public sealed class Dispatcher
{
    private readonly IServiceProvider _provider;

    public Dispatcher(IServiceProvider provider) => _provider = provider;

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => InvokeAsync<TResult>(query, typeof(IQueryHandler<,>), cancellationToken);

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        => InvokeAsync<TResult>(command, typeof(ICommandHandler<,>), cancellationToken);

    private Task<TResult> InvokeAsync<TResult>(object message, Type openHandler, CancellationToken cancellationToken)
    {
        var handlerType = openHandler.MakeGenericType(message.GetType(), typeof(TResult));
        var handler = _provider.GetService(handlerType)
                      ?? throw new InvalidOperationException($"No handler registered for {message.GetType().Name}.");
        var method = handlerType.GetMethod("HandleAsync")
                     ?? throw new InvalidOperationException("Handler missing HandleAsync.");
        return (Task<TResult>)method.Invoke(handler, new[] { message, cancellationToken })!;
    }
}
