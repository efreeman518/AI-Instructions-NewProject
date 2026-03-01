namespace TaskFlow.UI.Views;

public sealed partial class TeamListPage : Page
{
    public TeamListPage()
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
        if (sender is Button button && button.DataContext is TeamSummary item)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Team",
                Content = $"Are you sure you want to delete \"{item.Name}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var service = App.Host?.Services?.GetRequiredService<ITeamService>();
                var messenger = App.Host?.Services?.GetRequiredService<IMessenger>();
                if (service is not null && messenger is not null)
                {
                    await service.Delete(item.Id, CancellationToken.None);
                    messenger.Send(new Presentation.Messages.EntityMessage<TeamSummary>(Presentation.Messages.EntityChange.Deleted, item));
                }
            }
        }
    }
}
