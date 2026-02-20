# XAML Page Template

| | |
|---|---|
| **Files** | `Views/{Entity}ListPage.xaml`, `{Entity}DetailPage.xaml` + code-behind |
| **Depends on** | [mvux-model-template](mvux-model-template.md) |
| **Referenced by** | [uno-ui.md](../skills/uno-ui.md) |

## List Page

```xml
<Page x:Class="{Project}.UI.Views.{Entity}ListPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
      xmlns:utu="using:Uno.Toolkit.UI"
      xmlns:uen="using:Uno.Extensions.Navigation.UI"
      xmlns:ut="using:Uno.Themes"
      NavigationCacheMode="Enabled"
      Background="{ThemeResource BackgroundBrush}">

    <Page.Resources>
        <DataTemplate x:Key="EmptyTemplate">
            <utu:AutoLayout Padding="24"
                            Spacing="8"
                            PrimaryAxisAlignment="Center"
                            CounterAxisAlignment="Center">
                <TextBlock Text="No items yet"
                           Foreground="{ThemeResource OnSurfaceMediumBrush}"
                           Style="{StaticResource BodyMedium}" />
            </utu:AutoLayout>
        </DataTemplate>

        <!-- Item template for list cards -->
        <DataTemplate x:Key="{Entity}ItemTemplate">
            <utu:CardContentControl Margin="0"
                                    CornerRadius="4"
                                    Style="{StaticResource FilledCardContentControlStyle}">
                <utu:AutoLayout Background="{ThemeResource SurfaceBrush}"
                                CornerRadius="4"
                                Padding="16"
                                Spacing="8"
                                PrimaryAxisAlignment="Center"
                                HorizontalAlignment="Stretch">
                    <TextBlock Text="{Binding Name}"
                               Foreground="{ThemeResource OnSurfaceBrush}"
                               Style="{StaticResource TitleSmall}" />
                    <!-- Add more bound properties as needed -->
                </utu:AutoLayout>
            </utu:CardContentControl>
        </DataTemplate>
    </Page.Resources>

    <utu:AutoLayout utu:AutoLayout.PrimaryAlignment="Stretch">
        <!-- Navigation Bar -->
        <utu:NavigationBar Style="{StaticResource AppNavigationBarStyle}">
            <utu:NavigationBar.Content>
                <TextBlock Text="{Entity}s" Style="{StaticResource TitleLarge}" />
            </utu:NavigationBar.Content>
            <utu:NavigationBar.PrimaryCommands>
                <AppBarButton Command="{Binding Create}">
                    <AppBarButton.Icon>
                        <PathIcon Data="{StaticResource Icon_Add}" />
                    </AppBarButton.Icon>
                </AppBarButton>
            </utu:NavigationBar.PrimaryCommands>
        </utu:NavigationBar>

        <!-- Search Box -->
        <utu:AutoLayout Padding="16">
            <TextBox PlaceholderText="Search..."
                     Text="{Binding SearchTerm, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        </utu:AutoLayout>

        <!-- Feed-bound list -->
        <ScrollViewer utu:AutoLayout.PrimaryAlignment="Stretch">
            <utu:FeedView Source="{Binding FilteredItems}"
                          NoneTemplate="{StaticResource EmptyTemplate}">
                <DataTemplate>
                    <muxc:ItemsRepeater ItemsSource="{Binding Data}"
                                        uen:Navigation.Request="{Entity}Detail"
                                        uen:Navigation.Data="{Binding Data}"
                                        ItemTemplate="{StaticResource {Entity}ItemTemplate}">
                        <muxc:ItemsRepeater.Layout>
                            <muxc:StackLayout Spacing="8" />
                        </muxc:ItemsRepeater.Layout>
                    </muxc:ItemsRepeater>
                </DataTemplate>
            </uer:FeedView>
        </ScrollViewer>
    </utu:AutoLayout>
</Page>
```

## Detail Page

```xml
<Page x:Class="{Project}.UI.Views.{Entity}DetailPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
      xmlns:utu="using:Uno.Toolkit.UI"
      xmlns:uen="using:Uno.Extensions.Navigation.UI"
      xmlns:ut="using:Uno.Themes"
      Background="{ThemeResource BackgroundBrush}">

    <utu:AutoLayout utu:AutoLayout.PrimaryAlignment="Stretch">
        <!-- Navigation Bar with back -->
        <utu:NavigationBar Content="{Binding {Entity}.Name}"
                           Style="{StaticResource AppNavigationBarStyle}">
            <utu:NavigationBar.MainCommand>
                <AppBarButton uen:Navigation.Request="!back">
                    <AppBarButton.Icon>
                        <PathIcon Data="{StaticResource Icon_Back}" />
                    </AppBarButton.Icon>
                </AppBarButton>
            </utu:NavigationBar.MainCommand>
            <utu:NavigationBar.PrimaryCommands>
                <AppBarButton Command="{Binding ToggleFavorite}">
                    <AppBarButton.Icon>
                        <PathIcon Data="{StaticResource Icon_Heart}" />
                    </AppBarButton.Icon>
                </AppBarButton>
            </utu:NavigationBar.PrimaryCommands>
        </utu:NavigationBar>

        <ScrollViewer utu:AutoLayout.PrimaryAlignment="Stretch">
            <utu:AutoLayout Padding="16" Spacing="16">
                <!-- Entity header -->
                <TextBlock Text="{Binding {Entity}.Name}"
                           Style="{StaticResource HeadlineSmall}" />

                <!-- Child collection via FeedView -->
                <TextBlock Text="{ChildEntity}s"
                           Style="{StaticResource TitleMedium}"
                           Padding="0,16,0,8" />

                <utu:FeedView Source="{Binding {ChildEntity}Items}">
                    <DataTemplate>
                        <muxc:ItemsRepeater ItemsSource="{Binding Data}">
                            <muxc:ItemsRepeater.Layout>
                                <muxc:StackLayout Spacing="8" />
                            </muxc:ItemsRepeater.Layout>
                            <muxc:ItemsRepeater.ItemTemplate>
                                <DataTemplate>
                                    <utu:AutoLayout Padding="12"
                                                    CornerRadius="4"
                                                    Background="{ThemeResource SurfaceBrush}">
                                        <TextBlock Text="{Binding Name}"
                                                   Style="{StaticResource BodyMedium}" />
                                    </utu:AutoLayout>
                                </DataTemplate>
                            </muxc:ItemsRepeater.ItemTemplate>
                        </muxc:ItemsRepeater>
                    </DataTemplate>
                </uer:FeedView>
            </utu:AutoLayout>
        </ScrollViewer>
    </utu:AutoLayout>
</Page>
```

## Code-Behind (same for all pages)

```csharp
namespace {Project}.UI.Views;

public sealed partial class {Entity}ListPage : Page
{
    public {Entity}ListPage()
    {
        this.InitializeComponent();
    }
}
```

```csharp
namespace {Project}.UI.Views;

public sealed partial class {Entity}DetailPage : Page
{
    public {Entity}DetailPage()
    {
        this.InitializeComponent();
    }
}
```

## Rules

- Code-behind should be minimal — constructor + `InitializeComponent()` only
- All data binding targets the MVUX model (auto-set as `DataContext`)
- Use `utu:FeedView` to bind to `IFeed` / `IListFeed` / `IState` / `IListState`
- Wrap list data in `FeedView > DataTemplate > ItemsRepeater` pattern
- Use `uen:Navigation.Request` for declarative navigation on list items
- Use navigation qualifiers where needed: `-/` (escape nested stack), `!` (region/dialog), `!back` (back)
- Use `uen:Navigation.Data` to pass the selected item as navigation data
- Use `utu:AutoLayout` instead of raw `StackPanel`/`Grid` for layout
- Use `utu:Responsive` for adaptive breakpoints
- Reference Material theme resources: `{ThemeResource OnSurfaceBrush}`, `{ThemeResource SurfaceBrush}`, `{ThemeResource PrimaryBrush}`, etc.
- Use `{StaticResource TitleSmall}`, `{StaticResource BodyMedium}`, etc. for typography
- Register all custom `DataTemplate`s in `Page.Resources` or in `Views/Templates/`
