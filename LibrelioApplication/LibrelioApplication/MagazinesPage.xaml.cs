﻿using LibrelioApplication.Data;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234233

namespace LibrelioApplication
{
    /// <summary>
    /// A page that displays a collection of item previews.  In the Split Application this page
    /// is used to display and select one of the available groups.
    /// </summary>
    public sealed partial class ItemsPage : LibrelioApplication.Common.LayoutAwarePage
    {
        public ItemsPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            //var dataSrc = new LibrelioApplication.Data.MagazineDataSource();
            //this.DefaultViewModel["Items"] = dataSrc;
            //// TODO: Create an appropriate data model for your problem domain to replace the sample data
            //var sampleDataGroups = MagazineDataSource.GetGroups((String)navigationParameter);
            var sampleDataGroups = new MagazineDataSource(1);
            this.DefaultViewModel["Items"] = sampleDataGroups.AllMagazines;
        }

        /// <summary>
        /// Invoked when an item is clicked.
        /// </summary>
        /// <param name="sender">The GridView (or ListView when the application is snapped)
        /// displaying the item clicked.</param>
        /// <param name="e">Event data that describes the item clicked.</param>
        void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            //// Navigate to the appropriate destination page, configuring the new page
            //// by passing required information as a navigation parameter
            //var groupId = ((SampleDataGroup)e.ClickedItem).UniqueId;
            //this.Frame.Navigate(typeof(SplitPage), groupId);
        }

        private void itemGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
        }

        private T FindChild<T>(UIElement element, Func<T, bool> isObject)
                     where T : UIElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var uiElement = VisualTreeHelper.GetChild(element, i) as UIElement;
                var child = uiElement as T;
                if (child != null)
                {
                    if (isObject(child))
                        return child;
                }

                var result = FindChild(uiElement, isObject);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private List<T> FindChilds<T>(UIElement element, Func<T, bool> isObject, List<T> res = null)
                     where T : UIElement
        {
            if( res == null )
                res = new List<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var uiElement = VisualTreeHelper.GetChild(element, i) as UIElement;
                var child = uiElement as T;
                if (child != null)
                {
                    if (isObject(child))
                        res.Add(child);
                }

                FindChilds(uiElement, isObject, res);
            }
            return res;
        }


    }
}
