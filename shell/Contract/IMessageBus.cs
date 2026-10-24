namespace Fungtopia.Shell;

/// <summary>
/// Synchronous message bus between modules. Messages are plain structs passed by reference:
/// publishing does not allocate.
/// </summary>
public interface IMessageBus
{
    void Subscribe<T>(IMessageHandler<T> handler) where T : struct;

    /// <summary>Delivers the message to every subscriber before returning.</summary>
    void Publish<T>(in T message) where T : struct;
}

public interface IMessageHandler<T> where T : struct
{
    void Handle(in T message);
}
