using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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

        // ======== Topicok ========
        private ObservableCollection<Topic> _topics;
        private Topic _selectedTopic;
        private ObservableCollection<Event> _availableEvents;
        private ObservableCollection<Event> _filteredEvents;
        private Event _selectedEvent;
        private string _selectedPhotoPath;

        // Debounce / guardok az inkonzisztens interakciók ellen
        private DateTime _lastEventPickAt = DateTime.MinValue;
        private DateTime _lastTopicOpenAt = DateTime.MinValue;
        private bool _isOpeningTopic = false;
        private static readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(350);

        // ======== Barátkezelés ========
        private Guid _userId;
        private string _friendCode = string.Empty;
        private string _email = string.Empty;
        public ObservableCollection<Friend> Friends { get; set; } = new();
        public ObservableCollection<FriendRequest> IncomingRequests { get; set; } = new();

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

            // Real-time komment figyelés
            Task.Run(async () =>
            {
                try { await _supabaseClient.Realtime.ConnectAsync(); }
                catch (Exception ex)
                {
                    await Dispatcher.DispatchAsync(async () =>
                        await DisplayAlert("Hiba", $"Realtime kapcsolat inicializálása sikertelen: {ex.Message}", "OK"));
                }
            }).GetAwaiter().OnCompleted(() => SubscribeToComments());

            LoadUserDataAsync();
            LoadEventsAsync();
            LoadTopicsAsync();
            LoadFriendsAsync();
        }

        // =========================
        // ===== BARÁTKEZELÉS =====
        // =========================
        private async void LoadUserDataAsync()
        {
            try
            {
                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null) return;

                _email = user.Email ?? string.Empty;
                Guid.TryParse(user.Id, out _userId);

                var profileResp = await _supabaseClient.From<ProfileData>()
                    .Where(p => p.UserId == _userId).Get();

                var profile = profileResp.Models.FirstOrDefault();
                _friendCode = profile?.FriendCode ?? string.Empty;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Felhasználói adatok betöltése sikertelen: {ex.Message}", "OK");
            }
        }

        private async void LoadFriendsAsync()
        {
            try
            {
                Friends.Clear();
                IncomingRequests.Clear();

                var resp = await _supabaseClient.From<Friend>()
                    .Where(f => f.UserId == _userId || f.FriendUserId == _userId)
                    .Get();

                foreach (var f in resp.Models)
                {
                    if (f.IsAccepted)
                    {
                        var friendId = f.UserId == _userId ? f.FriendUserId : f.UserId;
                        var prof = await _supabaseClient.From<ProfileData>().Where(p => p.UserId == friendId).Get();
                        var pr = prof.Models.FirstOrDefault();
                        Friends.Add(new Friend
                        {
                            Id = f.Id,
                            UserId = f.UserId,
                            FriendUserId = f.FriendUserId,
                            FriendName = pr?.Description ?? pr?.FriendCode ?? "(ismeretlen)",
                            IsAccepted = true
                        });
                    }
                    else if (!f.IsAccepted && f.FriendUserId == _userId)
                    {
                        var prof = await _supabaseClient.From<ProfileData>().Where(p => p.UserId == f.UserId).Get();
                        var pr = prof.Models.FirstOrDefault();
                        IncomingRequests.Add(new FriendRequest
                        {
                            Id = f.Id,
                            SenderName = pr?.Description ?? pr?.FriendCode ?? "(ismeretlen)"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Barátok betöltése sikertelen: {ex.Message}", "OK");
            }
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
        }

        private async void OnFriendSearchCompleted(object sender, EventArgs e)
        {
            var code = FriendSearchEntry?.Text?.Trim();
            if (string.IsNullOrEmpty(code)) return;

            try
            {
                var res = await _supabaseClient.From<ProfileData>().Where(p => p.FriendCode == code).Get();
                var prof = res.Models.FirstOrDefault();
                if (prof == null)
                {
                    await DisplayAlert("Nincs találat", "Nem található ilyen barátkód.", "OK");
                    return;
                }

                var conf = await DisplayAlert("Barát hozzáadása",
                    $"Szeretnéd hozzáadni: {prof.Description ?? prof.FriendCode}", "Igen", "Nem");
                if (!conf) return;

                var friendGuid = prof.UserId;

                var existing = await _supabaseClient.From<Friend>()
                    .Where(f => (f.UserId == _userId && f.FriendUserId == friendGuid)
                             || (f.UserId == friendGuid && f.FriendUserId == _userId))
                    .Get();

                if (existing.Models.Any())
                {
                    await DisplayAlert("Hiba", "Már létezik kapcsolat vagy függõ kérelem.", "OK");
                    return;
                }

                var newReq = new Friend
                {
                    Id = Guid.NewGuid(),
                    UserId = _userId,
                    FriendUserId = friendGuid,
                    IsAccepted = false
                };
                await _supabaseClient.From<Friend>().Insert(newReq);
                await DisplayAlert("Siker", "Barátkérelem elküldve!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Keresés sikertelen: {ex.Message}", "OK");
            }
        }

        private async void OnAddFriendClicked(object sender, EventArgs e) => OnFriendSearchCompleted(sender, e);

        private async void OnAcceptFriendClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid id)
            {
                await _supabaseClient.From<Friend>().Where(f => f.Id == id).Set(f => f.IsAccepted, true).Update();
                LoadFriendsAsync();
            }
        }

        private async void OnDeclineFriendClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid id)
            {
                await _supabaseClient.From<Friend>().Where(f => f.Id == id).Delete();
                LoadFriendsAsync();
            }
        }

        private async void OnDeleteFriendClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid id)
            {
                await _supabaseClient.From<Friend>().Where(f => f.Id == id).Delete();
                LoadFriendsAsync();
            }
        }

        // =========================
        // ===== TOPICOK & KOMMENT =====
        // =========================
        private async void LoadEventsAsync()
        {
            try
            {
                var response = await _supabaseClient.From<Event>().Get();
                AvailableEvents = new ObservableCollection<Event>(response.Models);
                FilteredEvents = new ObservableCollection<Event>(AvailableEvents);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Események betöltése sikertelen: {ex.Message}", "OK");
            }
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

                    // kommentek
                    var commentResp = await _supabaseClient.From<TopicComment>()
                        .Where(c => c.TopicId == t.Id).Get();

                    topic.Comments = new ObservableCollection<TopicComment>(commentResp.Models);
                    topic.CommentCount = topic.Comments.Count;

                    // esemény link (ha nincs cache-ben, lekéri külön)
                    var eventLink = await _supabaseClient.From<TopicEvent>()
                        .Where(te => te.TopicId == t.Id).Single();

                    if (eventLink != null)
                    {
                        var evt = AvailableEvents.FirstOrDefault(e => e.Id == eventLink.EventId);
                        if (evt == null)
                        {
                            var evtResp = await _supabaseClient.From<Event>()
                                .Where(e => e.Id == eventLink.EventId).Single();
                            if (evtResp != null) evt = evtResp;
                        }

                        if (evt != null)
                        {
                            topic.LinkedEvent = evt;
                            topic.HasEvent = true;
                        }
                    }

                    // fotó
                    var photo = await _supabaseClient.From<TopicPhoto>()
                        .Where(tp => tp.TopicId == t.Id).Single();
                    if (photo != null)
                    {
                        topic.PhotoUrl = photo.PhotoUrl;
                        topic.HasPhoto = true;
                    }

                    topics.Add(topic);
                }

                Topics = new ObservableCollection<Topic>(topics.OrderByDescending(t => t.CreatedAt));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Topic-ok betöltése sikertelen: {ex.Message}", "OK");
            }
        }

        private async void SubscribeToComments()
        {
            try
            {
                var channel = _supabaseClient.Realtime.Channel("realtime:public:topic_comments");
                channel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.Inserts, async (sender, change) =>
                {
                    if (change.Payload?.Data == null) return;
                    var newComment = JsonSerializer.Deserialize<TopicComment>(change.Payload.Data.ToString());
                    if (newComment == null) return;

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
                await channel.Subscribe();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Realtime elõfizetés sikertelen: {ex.Message}", "OK");
            }
        }

        private void OnEventSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var s = (e.NewTextValue ?? string.Empty).Trim().ToLowerInvariant();
            var src = AvailableEvents ?? new ObservableCollection<Event>();
            FilteredEvents = new ObservableCollection<Event>(src.Where(ev =>
                (ev.EventName ?? string.Empty).ToLowerInvariant().Contains(s)));

            OnPropertyChanged(nameof(FilteredEvents));
        }

        // Stabilizált eseményválasztás — debounce + SelectedItem nullázás
        private async void OnEventSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (DateTime.UtcNow - _lastEventPickAt < _debounce) return;
                _lastEventPickAt = DateTime.UtcNow;

                if (e.CurrentSelection.FirstOrDefault() is Event sel)
                {
                    _selectedEvent = sel;
                    if (EventSearchEntry != null) EventSearchEntry.Text = sel.EventName;
                    FilteredEvents?.Clear();

                    // mindig nullázzuk a kijelölést
                    if (sender is CollectionView cv) cv.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Esemény kiválasztási hiba: {ex.Message}", "OK");
            }
        }

        private async void OnCreateTopicClicked(object sender, EventArgs e)
        {
            try
            {
                var title = TopicTitleEntry?.Text?.Trim();
                var desc = TopicDescriptionEditor?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(desc))
                {
                    await DisplayAlert("Hiba", "Adj meg címet és leírást!", "OK");
                    return;
                }

                var user = _supabaseClient.Auth.CurrentUser;
                if (user == null)
                {
                    await DisplayAlert("Hiba", "Jelentkezz be!", "OK");
                    return;
                }

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

                // ûrlap ürítés
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
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Kép kiválasztása sikertelen: {ex.Message}", "OK");
            }
        }

        // Ha SelectionChanged-et használsz XAML-ben:
        private async void OnTopicSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Topic t)
            {
                await OpenTopicAsync(t);
                if (sender is CollectionView cv) cv.SelectedItem = null; // mindig nullázzuk
            }
        }

        // Ha TapGestureRecognizer-t használsz XAML-ben:
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

                // (opcionálisan) frissítjük a kommenteket real-time-ra kész állapotba
                // de itt nem kell külön API hívás, mert már betöltöttük
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
                SelectedTopic.Comments.Add(added);
                if (CommentEditor != null) CommentEditor.Text = string.Empty;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hiba", $"Komment hiba: {ex.Message}", "OK");
            }
        }
    }

    // ======= MODELLEK =======
    [Table("friends")]
    public class Friend : BaseModel
    {
        [PrimaryKey("id", false)] public Guid Id { get; set; }
        [Column("user_id")] public Guid UserId { get; set; }
        [Column("friend_user_id")] public Guid FriendUserId { get; set; }
        [Column("is_accepted")] public bool IsAccepted { get; set; }
        public string FriendName { get; set; } = string.Empty;
    }

    public class FriendRequest
    {
        public Guid Id { get; set; }
        public string SenderName { get; set; } = string.Empty;
    }

    // ==== Topic modellek ====
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
