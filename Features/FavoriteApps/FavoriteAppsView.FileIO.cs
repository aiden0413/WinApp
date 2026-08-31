using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows; // Visibility 사용을 위해 추가

namespace winapp
{
    public static class FileIO
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "winapp",
            "favorites.json"
        );

        public class SaveDataModel
        {
            public List<TabDto> Tabs { get; set; } = new List<TabDto>();
        }

        public class TabDto
        {
            public string TabName { get; set; } = "탭";
            public List<ItemDto> Items { get; set; } = new List<ItemDto>();
        }

        public class ItemDto
        {
            public string AppName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public bool IsAddButton { get; set; }
        }

        public static void SaveTabs(IEnumerable<FavoriteTabItem> tabs)
        {
            try
            {
                var data = new SaveDataModel();

                foreach (var tab in tabs)
                {
                    if (tab.IsAddTabButton) continue;

                    var tabDto = new TabDto { TabName = tab.TabName };
                    foreach (var item in tab.Items)
                    {
                        if (item.IsAddButton) continue;

                        tabDto.Items.Add(new ItemDto
                        {
                            AppName = item.AppName,
                            FilePath = item.FilePath,
                            IsAddButton = item.IsAddButton
                        });
                    }
                    data.Tabs.Add(tabDto);
                }

                string? dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, jsonString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 저장 실패: {ex.Message}");
            }
        }

        public static List<FavoriteTabItem> LoadTabs()
        {
            var resultTabs = new List<FavoriteTabItem>();

            try
            {
                if (!File.Exists(FilePath)) return resultTabs;

                string jsonString = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<SaveDataModel>(jsonString);

                if (data?.Tabs != null)
                {
                    foreach (var tabDto in data.Tabs)
                    {
                        var tabItem = new FavoriteTabItem 
                        { 
                            TabName = tabDto.TabName,
                            IsAddTabButton = false // ★ 강제로 일반 탭임을 지정
                        };

                        foreach (var itemDto in tabDto.Items)
                        {
                            var icon = IconService.GetFileIcon(itemDto.FilePath);

                            tabItem.Items.Add(new FavoriteAppItem
                            {
                                AppName = itemDto.AppName,
                                FilePath = itemDto.FilePath,
                                AppIcon = icon,
                                IsAddButton = false,
                                NormalCardVisibility = Visibility.Visible,
                                AddButtonVisibility = Visibility.Collapsed,
                                DeleteButtonVisibility = Visibility.Collapsed
                            });
                        }

                        tabItem.Items.Add(new FavoriteAppItem
                        {
                            AppName = "추가",
                            FilePath = string.Empty,
                            AppIcon = null,
                            IsAddButton = true,
                            NormalCardVisibility = Visibility.Collapsed,
                            AddButtonVisibility = Visibility.Collapsed,
                            DeleteButtonVisibility = Visibility.Collapsed
                        });

                        resultTabs.Add(tabItem);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 로드 실패: {ex.Message}");
            }

            return resultTabs;
        }
    }
}