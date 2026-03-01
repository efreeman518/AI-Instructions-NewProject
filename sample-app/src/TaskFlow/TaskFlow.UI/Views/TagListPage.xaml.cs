namespace TaskFlow.UI.Views;

public sealed partial class TagListPage : Page
{
    public TagListPage()
    {
        this.InitializeComponent();
    }

    private void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        // MVUX Observe handles refresh via IMessenger — pull gesture completes immediately.
        var deferral = args.GetDeferral();
        deferral.Complete();
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TagSummary item)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Tag",
                Content = $"Are you sure you want to delete \"{item.Name}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var service = App.Host?.Services?.GetRequiredService<ITagService>();
                var messenger = App.Host?.Services?.GetRequiredService<IMessenger>();
                if (service is not null && messenger is not null)
                {
                    await service.Delete(item.Id, CancellationToken.None);
                    messenger.Send(new Presentation.Messages.EntityMessage<TagSummary>(Presentation.Messages.EntityChange.Deleted, item));
                }
            }
        }
    }
}
