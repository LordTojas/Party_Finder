using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;


using Newtonsoft.Json;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;


using MauiApp2.Models;

namespace MauiApp2.Pages
{
    public partial class CommunityPage : ContentPage
    {
        private readonly Supabase.Client _supabaseClient;

      
        private ObservableCollection<Topic> _topics;
        private Topic _selectedTopic;
        private ObservableCollection<Event> _availableEvents;
        private ObservableCollection<Event> _filteredEvents;
        private Event _selectedEvent;
        private string _selectedPhotoPath;

       
        private DateTime _lastEventPickAt = DateTime.MinValue;
        private DateTime _lastTopicOpenAt = DateTime.MinValue;
        private bool _isOpeningTopic = false;
        private static readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(350);

       
        private bool _isProgrammaticUpdate = false;

       
        private Guid _userId;
        private string _friendCode = string.Empty;
        private string _email = string.Empty;

      
        public ObservableCollection<Friend> Friends { get; set; } = new();
        public ObservableCollection<FriendRequest> IncomingRequests { get; set; } = new();

        
        private Guid _currentChatPartnerId;
        private RealtimeChannel _chatChannel;
        public ObservableCollection<PrivateMessage> CurrentChatMessages { get; set; } = new();

       
        public ObservableCollection<Topic> Topics
        {
            get => _topics;
            set { _topics = value; OnPropertyChanged(); }
        }

        public Topic SelectedTopic
        {
            get => _selectedTopic;
            set { _selectedTopic = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Event> AvailableEvents
        {
            get => _availableEvents;
            set { _availableEvents = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Event> FilteredEvents
        {
            get => _filteredEvents;
            set { _filteredEvents = value; OnPropertyChanged(); }
        }

        public CommunityPage(Supabase.Client supabaseClient)
        {
            InitializeComponent();
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            BindingContext = this;

            _topics = new();
            _availableEvents = new();
            _filteredEvents = new();

           
            Task.Run(async () =>
            {
                try { await _supabaseClient.Realtime.ConnectAsync(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Realtime hiba: {ex.Message}");
                }
            }).GetAwaiter().OnCompleted(() => SubscribeToComments());

            
            LoadUserDataAsync();
        }

        
        private async void LoadUserDataAsync()
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null) return;

                _email = user.Email ?? string.Empty;

                if (Guid.TryParse(user.Id, out Guid parsedId))
                {
                    _userId = parsedId;
                }
                else
                {
                    return;
                }

                var profileResp = await _supabaseClient.From<ProfileData>()
                    .Where(p => p.UserId == _userId).Get();

                var profile = profileResp.Models.FirstOrDefault();
                _friendCode = profile?.FriendCode ?? string.Empty;

                LoadEventsAsync();
                LoadTopicsAsync();
                LoadFriendsAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Felhasználói adatok betöltése sikertelen: {ex.Message}", "OK");
            }
        }

        
        private async void LoadFriendsAsync()
        {
            if (_userId == Guid.Empty) return;

            try
            {
                Friends.Clear();
                IncomingRequests.Clear();

                
                var t1 = _supabaseClient.From<Friend>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, _userId.ToString())
                    .Get();

                
                var t2 = _supabaseClient.From<Friend>()
                    .Filter("friend_user_id", Supabase.Postgrest.Constants.Operator.Equals, _userId.ToString())
                    .Get();

                await Task.WhenAll(t1, t2);
                var allResults = t1.Result.Models.Concat(t2.Result.Models).ToList();

                foreach (var f in allResults)
                {
                   
                    if (f.IsAccepted)
                    {
                        var friendId = (f.UserId == _userId) ? f.FriendUserId : f.UserId;

                       
                        var userResp = await _supabaseClient.From<MauiApp2.Models.User>()
                                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, friendId.ToString())
                                            .Get();

                        var userObj = userResp.Models.FirstOrDefault();
                        string displayName = userObj?.Username ?? "(Ismeretlen)";

                        if (!Friends.Any(existing => existing.Id == f.Id))
                        {
                            Friends.Add(new Friend
                            {
                                Id = f.Id,
                                UserId = f.UserId,
                                FriendUserId = f.FriendUserId,
                                IsAccepted = true,
                                FriendName = displayName
                            });
                        }
                    }
                    
                    else if (!f.IsAccepted && f.FriendUserId == _userId)
                    {
                        var userResp = await _supabaseClient.From<MauiApp2.Models.User>()
                                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, f.UserId.ToString())
                                            .Get();

                        var userObj = userResp.Models.FirstOrDefault();
                        string senderName = userObj?.Username ?? "(Ismeretlen)";

                        if (!IncomingRequests.Any(req => req.Id == f.Id))
                        {
                            IncomingRequests.Add(new FriendRequest
                            {
                                Id = f.Id,
                                SenderName = senderName
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Barátok betöltése sikertelen: {ex.Message}", "OK");
            }
        }

       

        private async void OnOpenChatClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid friendshipId)
            {
                var friend = Friends.FirstOrDefault(f => f.Id == friendshipId);
                if (friend == null) return;

               
                _currentChatPartnerId = (friend.UserId == _userId) ? friend.FriendUserId : friend.UserId;

                
                ChatPartnerNameLabel.Text = friend.FriendName;

                // Megjelenítjük az ablakot
                ChatOverlay.IsVisible = true;

                // Üzenetek betöltése és feliratkozás
                await LoadChatMessagesAsync();
                await SubscribeToChatAsync();
            }
        }

        private void OnCloseChatClicked(object sender, EventArgs e)
        {
            ChatOverlay.IsVisible = false;
            CurrentChatMessages.Clear();

            if (_chatChannel != null)
            {
                _chatChannel.Unsubscribe();
                _chatChannel = null;
            }
        }

        private async Task LoadChatMessagesAsync()
        {
            try
            {
                CurrentChatMessages.Clear();

               
                var oneHourAgo = DateTime.UtcNow.AddHours(-1);

                var response = await _supabaseClient.From<PrivateMessage>()
                    .Select("*")
                    .Filter("created_at", Supabase.Postgrest.Constants.Operator.GreaterThan, oneHourAgo.ToString("o"))
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                // Kliens oldali szûrés a két félre
                var messages = response.Models.Where(m =>
                    (m.SenderId == _userId && m.ReceiverId == _currentChatPartnerId) ||
                    (m.SenderId == _currentChatPartnerId && m.ReceiverId == _userId)
                ).ToList();

                foreach (var msg in messages)
                {
                    msg.IsMine = msg.SenderId == _userId;
                    CurrentChatMessages.Add(msg);
                }

                if (CurrentChatMessages.Count > 0)
                {
                    ChatMessagesCollectionView.ScrollTo(CurrentChatMessages.Last(), position: ScrollToPosition.End, animate: false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chat hiba: {ex.Message}");
            }
        }

        private async void OnSendMessageClicked(object sender, EventArgs e)
        {
            var text = ChatMessageEntry?.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                var msg = new PrivateMessage
                {
                    SenderId = _userId,
                    ReceiverId = _currentChatPartnerId,
                    MessageText = text,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabaseClient.From<PrivateMessage>().Insert(msg);

                if (ChatMessageEntry != null) ChatMessageEntry.Text = string.Empty;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", "Nem sikerült elküldeni.", "OK");
            }
        }

        private async Task SubscribeToChatAsync()
        {
            try
            {
                _chatChannel = _supabaseClient.Realtime.Channel("realtime:public:private_messages");

                _chatChannel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.Inserts, async (sender, change) =>
                {
                    if (change.Payload?.Data == null) return;

                    var newMsg = System.Text.Json.JsonSerializer.Deserialize<PrivateMessage>(change.Payload.Data.ToString());
                    if (newMsg == null) return;

                    bool isRelevant = (newMsg.SenderId == _userId && newMsg.ReceiverId == _currentChatPartnerId) ||
                                      (newMsg.SenderId == _currentChatPartnerId && newMsg.ReceiverId == _userId);

                    if (isRelevant)
                    {
                        await Dispatcher.DispatchAsync(() =>
                        {
                            newMsg.IsMine = newMsg.SenderId == _userId;
                            CurrentChatMessages.Add(newMsg);
                            ChatMessagesCollectionView.ScrollTo(newMsg, position: ScrollToPosition.End, animate: true);
                        });
                    }
                });

                _chatChannel.Subscribe();
            }
            catch { }
        }

        

        private void OnCommunityTabClicked(object sender, EventArgs e)
        {
            CommunityView.IsVisible = true;
            FriendsView.IsVisible = false;
            CommunityTabButton.BackgroundColor = Color.FromArgb("#9B59B6");
            FriendsTabButton.BackgroundColor = Colors.Transparent;
        }

        private void OnFriendsTabClicked(object sender, EventArgs e)
        {
            CommunityView.IsVisible = false;
            FriendsView.IsVisible = true;
            FriendsTabButton.BackgroundColor = Color.FromArgb("#9B59B6");
            CommunityTabButton.BackgroundColor = Colors.Transparent;

            LoadFriendsAsync();
        }

        

        private async void OnShowFriendCodeClicked(object sender, EventArgs e)
        {
            var code = string.IsNullOrWhiteSpace(_friendCode) ? "(nincs még létrehozva)" : _friendCode;
            await DisplayAlert("Saját barátkód", code, "OK");
            await Clipboard.Default.SetTextAsync(code);
        }

        private async void OnFriendSearchCompleted(object sender, EventArgs e)
        {
            var code = FriendSearchEntry?.Text?.Trim();
            if (string.IsNullOrEmpty(code)) return;

            if (_userId == Guid.Empty)
            {
                await DisplayAlert("Hiba", "Nem vagy bejelentkezve.", "OK");
                return;
            }

            try
            {
                
                var res = await _supabaseClient.From<ProfileData>().Where(p => p.FriendCode == code).Get();
                var targetProfile = res.Models.FirstOrDefault();

                if (targetProfile == null)
                {
                    await DisplayAlert("Nincs találat", "Nem található felhasználó ezzel a barátkóddal.", "OK");
                    return;
                }

                if (targetProfile.UserId == _userId)
                {
                    await DisplayAlert("Hiba", "Ez a saját barátkódod.", "OK");
                    return;
                }

                bool confirm = await DisplayAlert("Barát hozzáadása",
                    $"Szeretnéd hozzáadni õt (Barátkód: {targetProfile.FriendCode})?", "Igen", "Nem");
                if (!confirm) return;

                var targetUserId = targetProfile.UserId;

                
                var check1 = await _supabaseClient.From<Friend>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, _userId.ToString())
                    .Filter("friend_user_id", Supabase.Postgrest.Constants.Operator.Equals, targetUserId.ToString())
                    .Get();

                var check2 = await _supabaseClient.From<Friend>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, targetUserId.ToString())
                    .Filter("friend_user_id", Supabase.Postgrest.Constants.Operator.Equals, _userId.ToString())
                    .Get();

                if (check1.Models.Any() || check2.Models.Any())
                {
                    await DisplayAlert("Infó", "Már van köztetek kapcsolat.", "OK");
                    return;
                }

                
                var newRequest = new Friend
                {
                    UserId = _userId,
                    FriendUserId = targetUserId,
                    IsAccepted = false
                };

                await _supabaseClient.From<Friend>().Insert(newRequest);

                await DisplayAlert("Siker", "Barátkérelem elküldve!", "OK");
                if (FriendSearchEntry != null) FriendSearchEntry.Text = string.Empty;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Hiba: {ex.Message}", "OK");
            }
        }

        private void OnAddFriendClicked(object sender, EventArgs e) => OnFriendSearchCompleted(sender, e);

        private async void OnAcceptFriendClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid friendshipId)
            {
                try
                {
                    await _supabaseClient.From<Friend>()
                        .Where(f => f.Id == friendshipId)
                        .Set(f => f.IsAccepted, true)
                        .Update();

                    LoadFriendsAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Hiba", $"Hiba: {ex.Message}", "OK");
                }
            }
        }

        private async void OnDeclineFriendClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid friendshipId)
            {
                try
                {
                    await _supabaseClient.From<Friend>().Where(f => f.Id == friendshipId).Delete();
                    LoadFriendsAsync();
                }
                catch { }
            }
        }

        private async void OnDeleteFriendClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid friendshipId)
            {
                bool confirm = await DisplayAlert("Törlés", "Biztosan törlöd a barátot?", "Igen", "Mégse");
                if (!confirm) return;

                try
                {
                    await _supabaseClient.From<Friend>().Where(f => f.Id == friendshipId).Delete();
                    LoadFriendsAsync();
                }
                catch { }
            }
        }

     
        private async void LoadEventsAsync()
        {
            try
            {
                var response = await _supabaseClient.From<Event>().Get();
                AvailableEvents = new ObservableCollection<Event>(response.Models);
                FilteredEvents = new ObservableCollection<Event>(AvailableEvents);
            }
            catch { }
        }

       
        private async void LoadTopicsAsync()
        {
            try
            {
                var response = await _supabaseClient.From<TopicModel>().Get();
                var topicModels = response.Models;
                var topics = new List<Topic>();

                foreach (var t in topicModels)
                {
                    var topic = new Topic
                    {
                        Id = t.Id,
                        UserId = t.UserId,
                        Title = t.Title,
                        Description = t.Description,
                        CreatedAt = t.CreatedAt,
                        Comments = new ObservableCollection<TopicComment>()
                    };

                    var commentResp = await _supabaseClient.From<TopicComment>()
                        .Where(c => c.TopicId == t.Id).Get();

                    var commentsWithNames = new List<TopicComment>();
                    foreach (var c in commentResp.Models)
                    {
                       
                        var userResp = await _supabaseClient.From<MauiApp2.Models.User>()
                                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, c.UserId)
                                            .Get();
                        var user = userResp.Models.FirstOrDefault();

                        
                        c.UserName = user?.Username ?? "Névtelen felhasználó";
                        commentsWithNames.Add(c);
                    }

                    topic.Comments = new ObservableCollection<TopicComment>(commentsWithNames);
                    topic.CommentCount = topic.Comments.Count;

                    
                    try
                    {
                        var eventLinkResp = await _supabaseClient.From<TopicEvent>()
                            .Where(te => te.TopicId == t.Id).Get();
                        var eventLink = eventLinkResp.Models.FirstOrDefault();
                        if (eventLink != null)
                        {
                            var evt = AvailableEvents.FirstOrDefault(e => e.Id == eventLink.EventId);
                            if (evt != null)
                            {
                                topic.LinkedEvent = evt;
                                topic.HasEvent = true;
                            }
                        }
                    }
                    catch { }

                  
                    try
                    {
                        var photoResp = await _supabaseClient.From<TopicPhoto>()
                            .Where(tp => tp.TopicId == t.Id).Get();
                        var photo = photoResp.Models.FirstOrDefault();
                        if (photo != null)
                        {
                            topic.PhotoUrl = photo.PhotoUrl;
                            topic.HasPhoto = true;
                        }
                    }
                    catch { }

                    topics.Add(topic);
                }
                Topics = new ObservableCollection<Topic>(topics.OrderByDescending(t => t.CreatedAt));
            }
            catch { }
        }

        private async void SubscribeToComments()
        {
            try
            {
                var channel = _supabaseClient.Realtime.Channel("realtime:public:topic_comments");
                channel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.Inserts, async (sender, change) =>
                {
                    if (change.Payload?.Data == null) return;
                    var newComment = System.Text.Json.JsonSerializer.Deserialize<TopicComment>(change.Payload.Data.ToString());
                    if (newComment == null) return;

                    
                    try
                    {
                        var userResp = await _supabaseClient.From<MauiApp2.Models.User>()
                                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, newComment.UserId)
                                            .Get();
                        var user = userResp.Models.FirstOrDefault();
                        newComment.UserName = user?.Username ?? "Új hozzászóló";
                    }
                    catch { newComment.UserName = "Új hozzászóló"; }

                    var topic = Topics?.FirstOrDefault(t => t.Id == newComment.TopicId);
                    if (topic != null)
                    {
                        await Dispatcher.DispatchAsync(() =>
                        {
                            topic.Comments.Add(newComment);
                            topic.CommentCount = topic.Comments.Count;
                        });
                    }
                });

                channel.Subscribe();
            }
            catch { }
        }

        

        private void OnEventSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            
            if (_isProgrammaticUpdate) return;

            var s = (e.NewTextValue ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(s))
            {
                FilteredEvents = null;
                _selectedEvent = null;
                return;
            }

            var src = AvailableEvents ?? new ObservableCollection<Event>();
            var results = src.Where(ev =>
                (ev.EventName ?? string.Empty).ToLowerInvariant().Contains(s)).ToList();

            
            FilteredEvents = results.Any() ? new ObservableCollection<Event>(results) : null;
            OnPropertyChanged(nameof(FilteredEvents));
        }

        private async void OnEventSuggestionTapped(object sender, EventArgs e)
        {
            try
            {
                
                if (sender is BindableObject bo && bo.BindingContext is Event sel)
                {
                    _selectedEvent = sel;

                    
                    _isProgrammaticUpdate = true;

                    if (EventSearchEntry != null)
                    {
                        EventSearchEntry.Text = sel.EventName;
                        
                        try { EventSearchEntry.CursorPosition = sel.EventName.Length; } catch { }
                        
                        EventSearchEntry.Unfocus();
                    }

                    _isProgrammaticUpdate = false;

                    
                    FilteredEvents = null;
                    OnPropertyChanged(nameof(FilteredEvents));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Esemény választási hiba: {ex.Message}");
            }
        }

       

        private async void OnCreateTopicClicked(object sender, EventArgs e)
        {
            try
            {
                var title = TopicTitleEntry?.Text?.Trim();
                var desc = TopicDescriptionEditor?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(desc)) return;

                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null) return;

                var topicModel = new TopicModel
                {
                    UserId = user.Id,
                    Title = title,
                    Description = desc,
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await _supabaseClient.From<TopicModel>().Insert(topicModel);
                var topic = inserted.Models.First();

                if (_selectedEvent != null)
                    await _supabaseClient.From<TopicEvent>().Insert(new TopicEvent { TopicId = topic.Id, EventId = _selectedEvent.Id });

                if (!string.IsNullOrEmpty(_selectedPhotoPath))
                {
                    var bytes = await File.ReadAllBytesAsync(_selectedPhotoPath);
                    var fileName = $"{topic.Id}_{DateTime.Now.Ticks}.jpg";
                    await _supabaseClient.Storage.From("topic-photos").Upload(bytes, fileName);
                    var url = _supabaseClient.Storage.From("topic-photos").GetPublicUrl(fileName);
                    await _supabaseClient.From<TopicPhoto>().Insert(new TopicPhoto { TopicId = topic.Id, PhotoUrl = url });
                }

                if (TopicTitleEntry != null) TopicTitleEntry.Text = string.Empty;
                if (TopicDescriptionEditor != null) TopicDescriptionEditor.Text = string.Empty;
                if (EventSearchEntry != null) EventSearchEntry.Text = string.Empty;
                _selectedEvent = null;
                _selectedPhotoPath = null;

                LoadTopicsAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Létrehozás sikertelen: {ex.Message}", "OK");
            }
        }

        private async void OnSelectPhotoClicked(object sender, EventArgs e)
        {
            try
            {
                var file = await FilePicker.PickAsync(new PickOptions { FileTypes = FilePickerFileType.Images });
                if (file != null) _selectedPhotoPath = file.FullPath;
            }
            catch { }
        }

        private async void OnTopicSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Topic t)
            {
                await OpenTopicAsync(t);
                if (sender is CollectionView cv) cv.SelectedItem = null;
            }
        }

        private async void OnTopicTapped(object sender, EventArgs e)
        {
            if (sender is BindableObject bo && bo.BindingContext is Topic t)
                await OpenTopicAsync(t);
        }

        private async Task OpenTopicAsync(Topic t)
        {
            try
            {
                if (t == null) return;
                if (_isOpeningTopic) return;
                if (DateTime.UtcNow - _lastTopicOpenAt < _debounce) return;

                _isOpeningTopic = true;
                _lastTopicOpenAt = DateTime.UtcNow;

                SelectedTopic = t;
                CommunityView.IsVisible = false;
                DetailFrame.IsVisible = true;
            }
            finally
            {
                _isOpeningTopic = false;
            }
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
            CommunityView.IsVisible = true;
            DetailFrame.IsVisible = false;
            SelectedTopic = null;
        }

        private async void OnAddCommentClicked(object sender, EventArgs e)
        {
            try
            {
                var txt = CommentEditor?.Text?.Trim();
                if (string.IsNullOrEmpty(txt)) return;

                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null || SelectedTopic == null) return;

                var c = new TopicComment
                {
                    TopicId = SelectedTopic.Id,
                    UserId = user.Id,
                    CommentText = txt,
                    CreatedAt = DateTime.UtcNow
                };
                var res = await _supabaseClient.From<TopicComment>().Insert(c);

                
                var added = res.Models.First();

                
                try
                {
                    var userResp = await _supabaseClient.From<MauiApp2.Models.User>()
                                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.Id)
                                        .Get();
                    added.UserName = userResp.Models.FirstOrDefault()?.Username ?? "Én";
                }
                catch { added.UserName = "Én"; }

                SelectedTopic.Comments.Add(added);
                if (CommentEditor != null) CommentEditor.Text = string.Empty;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Komment hiba: {ex.Message}", "OK");
            }
        }
    }

   

    [Table("friends")]
    public class Friend : BaseModel
    {
        [PrimaryKey("id", false)] public Guid Id { get; set; }
        [Column("user_id")] public Guid UserId { get; set; }
        [Column("friend_user_id")] public Guid FriendUserId { get; set; }
        [Column("is_accepted")] public bool IsAccepted { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public string FriendName { get; set; } = string.Empty;
    }

    public class FriendRequest
    {
        public Guid Id { get; set; }
        public string SenderName { get; set; } = string.Empty;
    }

    [Table("topics")]
    public class TopicModel : BaseModel
    {
        [PrimaryKey("id", false)] public Guid Id { get; set; }
        [Column("user_id")] public string UserId { get; set; } = string.Empty;
        [Column("title")] public string Title { get; set; } = string.Empty;
        [Column("description")] public string Description { get; set; } = string.Empty;
        [Column("created_at")] public DateTime CreatedAt { get; set; }
    }

    public class Topic
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public ObservableCollection<TopicComment> Comments { get; set; } = new();
        public int CommentCount { get; set; }
        public Event? LinkedEvent { get; set; }
        public bool HasEvent { get; set; }
        public string? PhotoUrl { get; set; }
        public bool HasPhoto { get; set; }
    }

    [Table("topic_comments")]
    public class TopicComment : BaseModel
    {
        [PrimaryKey("id", false)] public Guid Id { get; set; }
        [Column("topic_id")] public Guid TopicId { get; set; }
        [Column("user_id")] public string UserId { get; set; } = string.Empty;
        [Column("comment_text")] public string CommentText { get; set; } = string.Empty;
        [Column("created_at")] public DateTime CreatedAt { get; set; }

        
        [Newtonsoft.Json.JsonIgnore]
        public string UserName { get; set; } = string.Empty;
    }

    [Table("topic_events")]
    public class TopicEvent : BaseModel
    {
        [PrimaryKey("id", false)] public Guid Id { get; set; }
        [Column("topic_id")] public Guid TopicId { get; set; }
        [Column("event_id")] public long EventId { get; set; }
    }

    [Table("topic_photos")]
    public class TopicPhoto : BaseModel
    {
        [PrimaryKey("id", false)] public Guid Id { get; set; }
        [Column("topic_id")] public Guid TopicId { get; set; }
        [Column("photo_url")] public string PhotoUrl { get; set; } = string.Empty;
    }
}