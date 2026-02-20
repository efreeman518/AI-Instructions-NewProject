// Pattern: TodoItemListPage code-behind — minimal.
// All logic lives in TodoItemListModel (MVUX).
// ItemClick handler bridges ListView selection to NavigateToDetail command.

namespace TaskFlow.UI.Views;

public sealed partial class TodoItemListPage : Page
{
    public TodoItemListPage() => InitializeComponent();
}
