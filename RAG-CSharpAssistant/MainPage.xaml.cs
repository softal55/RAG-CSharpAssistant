using System.Collections.Specialized;
using RAG_CSharpAssistant.Models;
using RAG_CSharpAssistant.ViewModels;

namespace RAG_CSharpAssistant;

public partial class MainPage : ContentPage
{
    private MainPageViewModel? _viewModel;

    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
        SizeChanged += OnSizeChanged;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
            _viewModel.MessageStreamed -= OnMessageStreamed;
        }

        _viewModel = BindingContext as MainPageViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
            _viewModel.MessageStreamed += OnMessageStreamed;
            UpdateEmptyChatVisibility();
        }
    }

    private void UpdateEmptyChatVisibility()
    {
        if (_viewModel is null) return;

        var isEmpty = _viewModel.Messages.Count == 0;
        EmptyState.IsVisible = isEmpty;
        ChatList.IsVisible = !isEmpty;
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (_viewModel is null || Width <= 0) return;
        _viewModel.MaxBubbleWidth = Width * 0.8;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateEmptyChatVisibility();

        if (e.Action == NotifyCollectionChangedAction.Add)
            ScrollToBottom();
    }

    private void OnMessageStreamed(ChatMessage _) => ScrollToBottom();

    private void ScrollToBottom()
    {
        if (_viewModel is null || _viewModel.Messages.Count == 0) return;

        // Marshal to the UI thread; CollectionView.ScrollTo must run there.
        Dispatcher.Dispatch(() =>
        {
            try
            {
                ChatList.ScrollTo(
                    index: _viewModel.Messages.Count - 1,
                    position: ScrollToPosition.End,
                    animate: true);
            }
            catch
            {
                // ScrollTo can throw if the item isn't realized yet; ignore.
            }
        });
    }
}
