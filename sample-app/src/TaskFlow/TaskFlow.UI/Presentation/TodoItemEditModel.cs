namespace TaskFlow.UI.Presentation;

/// <summary>
/// TodoItem edit model — handles creating new and editing existing todo items.
/// When navigated with a TodoItemSummary data parameter, it edits; otherwise it creates.
/// Uses the MVUX Command pattern (Given/When/Then) for save, like LoginModel.
/// </summary>
public partial record TodoItemEditModel
{
    private readonly INavigator _navigator;
    private readonly ITodoItemService _todoItemService;
    private readonly ICategoryService _categoryService;
    private readonly IMessenger _messenger;
    private readonly bool _isNew;

    public TodoItemEditModel(
        INavigator navigator,
        ITodoItemService todoItemService,
        ICategoryService categoryService,
        IMessenger messenger,
        TodoItemSummary? item = null)
    {
        _navigator = navigator;
        _todoItemService = todoItemService;
        _categoryService = categoryService;
        _messenger = messenger;
        _isNew = item is null;
        _editItem = item ?? new TodoItemSummary();
    }

    private readonly TodoItemSummary _editItem;

    /// <summary>Whether this is a new item (create) vs editing an existing one.</summary>
    public bool IsNew => _isNew;

    /// <summary>Page title — "New Task" or "Edit Task".</summary>
    public string PageTitle => _isNew ? "New Task" : "Edit Task";

    /// <summary>The item being edited — single state with two-way bound properties via MVUX proxy.</summary>
    public IState<TodoItemSummary> Item => State.Value(this, () => _editItem);

    /// <summary>Available categories for picker.</summary>
    public IListFeed<CategorySummary> Categories => ListFeed.Async(_categoryService.GetAll);

    /// <summary>Save command — reads current Item state via Given/When/Then pattern.</summary>
    public ICommand Save => Command.Create(b => b
        .Given(Item)
        .When(item => item is not null && !string.IsNullOrWhiteSpace(item.Title))
        .Then(DoSave));

    private async ValueTask DoSave(TodoItemSummary item, CancellationToken ct)
    {
        TodoItemSummary? result;
        if (_isNew)
        {
            item = item with { Id = Guid.NewGuid() };
            result = await _todoItemService.Create(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<TodoItemSummary>(EntityChange.Created, result));
        }
        else
        {
            result = await _todoItemService.Update(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<TodoItemSummary>(EntityChange.Updated, result));
        }

        await _navigator.NavigateBackAsync(this, cancellation: ct);
    }

    /// <summary>Cancel and go back.</summary>
    public async ValueTask Cancel(CancellationToken ct) =>
        await _navigator.NavigateBackAsync(this, cancellation: ct);
}

/// <summary>Priority option for picker display.</summary>
public sealed record PriorityOption(int Value, string Label)
{
    public override string ToString() => Label;
}
