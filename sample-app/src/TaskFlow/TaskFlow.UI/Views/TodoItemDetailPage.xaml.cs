namespace TaskFlow.UI.Views;

public sealed partial class TodoItemDetailPage : Page
{
    public TodoItemDetailPage()
    {
        this.InitializeComponent();
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        // Extract the current item title from the detail heading TextBlock.
        // The DataContext is the MVUX-generated BindableTodoItemDetailModel proxy.
        // Use dynamic dispatch to access the Item property without referencing generated types.
        TodoItemSummary? item = null;
        try
        {
            item = ((dynamic)DataContext!).Item as TodoItemSummary;
        }
        catch
        {
            // Fallback — unable to access Item from proxy
            return;
        }

        if (item is null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Task",
            Content = $"Are you sure you want to delete \"{item.Title}\"?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var service = App.Host?.Services?.GetRequiredService<ITodoItemService>();
            var messenger = App.Host?.Services?.GetRequiredService<IMessenger>();
            var navigator = App.Host?.Services?.GetRequiredService<INavigator>();
            if (service is not null && messenger is not null)
            {
                await service.Delete(item.Id, CancellationToken.None);
                messenger.Send(new Presentation.Messages.EntityMessage<TodoItemSummary>(Presentation.Messages.EntityChange.Deleted, item));
                // Navigate back after deletion
                if (navigator is not null)
                {
                    await navigator.NavigateBackAsync(this);
                }
            }
        }
    }
}
