namespace TaskFlow.UI.Presentation.Messages;

/// <summary>
/// Cross-model entity change message — used with IMessenger for reactive refresh.
/// Follows Uno.Extensions.Reactive.Messaging pattern with EntityMessage&lt;T&gt;.
/// </summary>
public record EntityMessage<T>(EntityChange Change, T Entity);

/// <summary>
/// Type of entity change for cross-model messaging.
/// </summary>
public enum EntityChange
{
    Created,
    Updated,
    Deleted,
}
