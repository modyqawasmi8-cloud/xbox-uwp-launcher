using System;
using System.Collections.ObjectModel;
using Windows.Data.Json;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace XboxUwpLauncher
{
    public sealed partial class MainPage : Page
    {
        private const string EntriesSettingKey = "LauncherEntries";
        private readonly ObservableCollection<LauncherEntry> entries = new ObservableCollection<LauncherEntry>();
        private LauncherEntry editingEntry;

        public MainPage()
        {
            InitializeComponent();
            EntriesList.ItemsSource = entries;
            LoadEntries();
            UpdateEmptyState();
        }

        private void LoadEntries()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var saved = settings.Values[EntriesSettingKey] as string;
            if (string.IsNullOrWhiteSpace(saved))
            {
                return;
            }

            try
            {
                var array = JsonArray.Parse(saved);
                foreach (var value in array)
                {
                    var item = value.GetObject();
                    string name;
                    string launchUri;

                    if (item.TryGetValue("name", out var nameValue) &&
                        item.TryGetValue("uri", out var uriValue))
                    {
                        name = nameValue.GetString();
                        launchUri = uriValue.GetString();
                        if (!string.IsNullOrWhiteSpace(name) && IsAbsoluteUri(launchUri))
                        {
                            entries.Add(new LauncherEntry(name, launchUri));
                        }
                    }
                }
            }
            catch (Exception)
            {
                settings.Values.Remove(EntriesSettingKey);
                EmptyState.Text = "تعذّر قراءة القائمة المحفوظة. أضف تطبيقاتك من جديد.";
            }
        }

        private void SaveEntries()
        {
            var array = new JsonArray();
            foreach (var entry in entries)
            {
                var item = new JsonObject
                {
                    ["name"] = JsonValue.CreateStringValue(entry.Name),
                    ["uri"] = JsonValue.CreateStringValue(entry.LaunchUri)
                };
                array.Add(item);
            }

            ApplicationData.Current.LocalSettings.Values[EntriesSettingKey] = array.Stringify();
        }

        private async void SaveEntry_Click(object sender, RoutedEventArgs e)
        {
            var name = NameInput.Text.Trim();
            var launchUri = UriInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                await ShowMessage("اكتب اسم التطبيق أولاً.");
                NameInput.Focus(FocusState.Programmatic);
                return;
            }

            if (!IsAbsoluteUri(launchUri))
            {
                await ShowMessage("رابط URI غير صالح. استخدم رابطاً كاملاً مثل myapp://open.");
                UriInput.Focus(FocusState.Programmatic);
                return;
            }

            if (editingEntry == null)
            {
                entries.Add(new LauncherEntry(name, launchUri));
            }
            else
            {
                editingEntry.Name = name;
                editingEntry.LaunchUri = launchUri;
                editingEntry = null;
                EntriesList.ItemsSource = null;
                EntriesList.ItemsSource = entries;
                FormTitle.Text = "إضافة تطبيق";
                SaveButton.Content = "إضافة";
                CancelEditButton.Visibility = Visibility.Collapsed;
            }

            SaveEntries();
            NameInput.Text = string.Empty;
            UriInput.Text = string.Empty;
            UpdateEmptyState();
            EntriesList.Focus(FocusState.Programmatic);
        }

        private void EditEntry_Click(object sender, RoutedEventArgs e)
        {
            var entry = (sender as Button)?.Tag as LauncherEntry;
            if (entry == null)
            {
                return;
            }

            editingEntry = entry;
            NameInput.Text = entry.Name;
            UriInput.Text = entry.LaunchUri;
            FormTitle.Text = "تعديل التطبيق";
            SaveButton.Content = "حفظ";
            CancelEditButton.Visibility = Visibility.Visible;
            NameInput.Focus(FocusState.Programmatic);
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ResetEditor();
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            var entry = (sender as Button)?.Tag as LauncherEntry;
            if (entry == null)
            {
                return;
            }

            entries.Remove(entry);
            if (ReferenceEquals(editingEntry, entry))
            {
                ResetEditor();
            }

            SaveEntries();
            UpdateEmptyState();
        }

        private async void LaunchEntry_Click(object sender, RoutedEventArgs e)
        {
            var entry = (sender as Button)?.Tag as LauncherEntry;
            if (entry == null || !Uri.TryCreate(entry.LaunchUri, UriKind.Absolute, out var uri))
            {
                await ShowMessage("رابط التشغيل غير صالح.");
                return;
            }

            try
            {
                var launched = await Launcher.LaunchUriAsync(uri);
                if (!launched)
                {
                    await ShowMessage("لم يتم العثور على تطبيق يدعم هذا الرابط. تأكد من URI ومن دعم التطبيق لـ Xbox.");
                }
            }
            catch (Exception)
            {
                await ShowMessage("تعذّر تشغيل الرابط. قد لا يكون هناك تطبيق مسجّل له أو قد لا يسمح Xbox بتشغيله.");
            }
        }

        private async System.Threading.Tasks.Task ShowMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "مشغّل التطبيقات",
                Content = message,
                CloseButtonText = "حسناً"
            };
            await dialog.ShowAsync();
        }

        private static bool IsAbsoluteUri(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   !string.IsNullOrWhiteSpace(uri.Scheme);
        }

        private void ResetEditor()
        {
            editingEntry = null;
            NameInput.Text = string.Empty;
            UriInput.Text = string.Empty;
            FormTitle.Text = "إضافة تطبيق";
            SaveButton.Content = "إضافة";
            CancelEditButton.Visibility = Visibility.Collapsed;
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public sealed class LauncherEntry
    {
        public LauncherEntry(string name, string launchUri)
        {
            Name = name;
            LaunchUri = launchUri;
        }

        public string Name { get; set; }
        public string LaunchUri { get; set; }
    }
}
