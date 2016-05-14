﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Acr.UserDialogs;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Models.Page;
using Bit.App.Resources;
using Xamarin.Forms;
using XLabs.Ioc;

namespace Bit.App.Pages
{
    public class VaultListPage : ContentPage
    {
        private readonly IFolderService _folderService;
        private readonly ISiteService _siteService;
        private readonly IUserDialogs _userDialogs;
        private readonly IClipboardService _clipboardService;

        public VaultListPage()
        {
            _folderService = Resolver.Resolve<IFolderService>();
            _siteService = Resolver.Resolve<ISiteService>();
            _userDialogs = Resolver.Resolve<IUserDialogs>();
            _clipboardService = Resolver.Resolve<IClipboardService>();

            Init();
        }

        public ObservableCollection<VaultListPageModel.Folder> Folders { get; private set; } = new ObservableCollection<VaultListPageModel.Folder>();

        private void Init()
        {
            ToolbarItems.Add(new AddSiteToolBarItem(this));

            var listView = new ListView
            {
                IsGroupingEnabled = true,
                ItemsSource = Folders,
                HasUnevenRows = true,
                SeparatorColor = Color.FromHex("E0E0E0")
            };
            listView.GroupDisplayBinding = new Binding("Name");
            listView.GroupHeaderTemplate = new DataTemplate(() => new VaultListHeaderViewCell(this));
            listView.ItemSelected += SiteSelected;
            listView.ItemTemplate = new DataTemplate(() => new VaultListViewCell(this));

            Title = AppResources.MyVault;
            Content = listView;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadFoldersAsync().Wait();
        }

        private async Task LoadFoldersAsync()
        {
            Folders.Clear();

            var folders = await _folderService.GetAllAsync();
            var sites = await _siteService.GetAllAsync();

            foreach(var folder in folders)
            {
                var f = new VaultListPageModel.Folder(folder, sites.Where(s => s.FolderId == folder.Id));
                Folders.Add(f);
            }

            // add the sites with no folder
            var noneFolder = new VaultListPageModel.Folder(sites.Where(s => s.FolderId == null));
            Folders.Add(noneFolder);
        }

        private void SiteSelected(object sender, SelectedItemChangedEventArgs e)
        {
            var site = e.SelectedItem as VaultListPageModel.Site;
            var page = new ExtendedNavigationPage(new VaultViewSitePage(site.Id));
            Navigation.PushModalAsync(page);
        }

        private async void MoreClickedAsync(object sender, EventArgs e)
        {
            var mi = sender as MenuItem;
            var site = mi.CommandParameter as VaultListPageModel.Site;
            var selection = await DisplayActionSheet(AppResources.MoreOptions, AppResources.Cancel, null,
                AppResources.View, AppResources.Edit, AppResources.CopyPassword, AppResources.CopyUsername, AppResources.GoToWebsite);

            if(selection == AppResources.View)
            {
                var page = new ExtendedNavigationPage(new VaultViewSitePage(site.Id));
                await Navigation.PushModalAsync(page);
            }
            else if(selection == AppResources.Edit)
            {
                var page = new ExtendedNavigationPage(new VaultEditSitePage(site.Id));
                await Navigation.PushModalAsync(page);
            }
            else if(selection == AppResources.CopyPassword)
            {
                Copy(site.Password, AppResources.Password);
            }
            else if(selection == AppResources.CopyUsername)
            {
                Copy(site.Username, AppResources.Username);
            }
            else if(selection == AppResources.GoToWebsite)
            {
                Device.OpenUri(new Uri(site.Uri));
            }
        }

        private void Copy(string copyText, string alertLabel)
        {
            _clipboardService.CopyToClipboard(copyText);
            _userDialogs.SuccessToast(string.Format(AppResources.ValueHasBeenCopied, alertLabel));
        }

        private async void DeleteClickedAsync(object sender, EventArgs e)
        {
            if(!await _userDialogs.ConfirmAsync(AppResources.DoYouReallyWantToDelete, null, AppResources.Yes, AppResources.No))
            {
                return;
            }

            var mi = sender as MenuItem;
            var site = mi.CommandParameter as VaultListPageModel.Site;
            var deleteCall = await _siteService.DeleteAsync(site.Id);

            if(deleteCall.Succeeded)
            {
                var folder = Folders.Single(f => f.Id == site.FolderId);
                var siteIndex = folder.Select((s, i) => new { s, i }).First(s => s.s.Id == site.Id).i;
                folder.RemoveAt(siteIndex);
                _userDialogs.SuccessToast(AppResources.SiteDeleted);
            }
            else if(deleteCall.Errors.Count() > 0)
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred, deleteCall.Errors.First().Message, AppResources.Ok);
            }
        }

        private class AddSiteToolBarItem : ToolbarItem
        {
            private readonly VaultListPage _page;

            public AddSiteToolBarItem(VaultListPage page)
            {
                _page = page;
                Text = AppResources.Add;
                Icon = "ion-plus";
                Clicked += ClickedItem;
            }

            private async void ClickedItem(object sender, EventArgs e)
            {
                var page = new ExtendedNavigationPage(new VaultAddSitePage());
                await _page.Navigation.PushModalAsync(page);
            }
        }

        private class VaultListViewCell : TextCell
        {
            public VaultListViewCell(VaultListPage page)
            {
                var moreAction = new MenuItem { Text = AppResources.More };
                moreAction.SetBinding(MenuItem.CommandParameterProperty, new Binding("."));
                moreAction.Clicked += page.MoreClickedAsync;

                var deleteAction = new MenuItem { Text = AppResources.Delete, IsDestructive = true };
                deleteAction.SetBinding(MenuItem.CommandParameterProperty, new Binding("."));
                deleteAction.Clicked += page.DeleteClickedAsync;

                this.SetBinding<VaultListPageModel.Site>(TextProperty, s => s.Name);
                this.SetBinding<VaultListPageModel.Site>(DetailProperty, s => s.Username);
                ContextActions.Add(moreAction);
                ContextActions.Add(deleteAction);

                TextColor = Color.FromHex("333333");
                DetailColor = Color.FromHex("777777");
            }
        }

        private class VaultListHeaderViewCell : ViewCell
        {
            public VaultListHeaderViewCell(VaultListPage page)
            {
                var image = new Image
                {
                    Source = ImageSource.FromFile("fa-folder-open.png"),
                    Margin = new Thickness(16, 8, 0, 8)
                };

                var label = new Label
                {
                    FontSize = 14,
                    TextColor = Color.FromHex("777777"),
                    VerticalTextAlignment = TextAlignment.Center
                };

                label.SetBinding<VaultListPageModel.Folder>(Label.TextProperty, s => s.Name);

                var stackLayout = new StackLayout
                {
                    Orientation = StackOrientation.Horizontal,
                    BackgroundColor = Color.FromHex("ecf0f5")
                };

                stackLayout.Children.Add(image);
                stackLayout.Children.Add(label);

                var borderStackLayout = new StackLayout
                {
                    Spacing = 0
                };

                var borderBoxTop = new BoxView { BackgroundColor = Color.FromHex("d2d6de"), HeightRequest = 0.5 };
                var borderBoxBottom = new BoxView { BackgroundColor = Color.FromHex("d2d6de"), HeightRequest = 0.5 };
                borderStackLayout.Children.Add(borderBoxTop);
                borderStackLayout.Children.Add(stackLayout);
                borderStackLayout.Children.Add(borderBoxBottom);

                View = borderStackLayout;
                Height = 30;
            }
        }
    }
}
