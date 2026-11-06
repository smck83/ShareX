#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

#nullable enable

using Avalonia.Controls;
using Avalonia.Interactivity;
using ShareX.AvaloniaUI.Theming;
using ShareX.Localization;
using System;

namespace ShareX;

public partial class UploadConsentWindow : Window
{
    /// <summary>
    /// Defaults to <see cref="UploadConsentChoice.Cancel"/>, so closing the window with the
    /// title bar, Escape or anything else declines the upload.
    /// </summary>
    public UploadConsentChoice Choice { get; private set; } = UploadConsentChoice.Cancel;

    public bool RememberChoice => RememberChoiceCheckBox.IsChecked == true;

    public UploadConsentWindow() : this(null, null)
    {
    }

    public UploadConsentWindow(string? fileName, string? destination)
    {
        InitializeComponent();
        RequestedThemeVariant = ThemeManager.GetCurrentTheme();

        FileNameText.Text = string.IsNullOrEmpty(fileName) ? Strings.UploadConsentWindow_UnknownFile : fileName;
        DestinationText.Text = string.IsNullOrEmpty(destination)
            ? Strings.UploadConsentWindow_UnknownDestination : destination;

        UpdateRememberHint();

        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Activate();

        // Focus the declining button so that an immediate Enter or Space cannot authorize an upload.
        CancelButton.Focus();
    }

    private void OnRememberChoiceChanged(object? sender, RoutedEventArgs e) => UpdateRememberHint();

    /// <summary>
    /// Spells out what remembering the decision will actually change, because the consequence
    /// differs depending on which button is then pressed.
    /// </summary>
    private void UpdateRememberHint()
    {
        RememberHintText.Text = RememberChoice ? Strings.UploadConsentWindow_DontAskAgainHint : string.Empty;
        RememberHintText.IsVisible = RememberChoice;
    }

    private void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        Choice = UploadConsentChoice.Upload;
        Close();
    }

    private void OnNeverUploadClick(object? sender, RoutedEventArgs e)
    {
        Choice = UploadConsentChoice.NeverUpload;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Choice = UploadConsentChoice.Cancel;
        Close();
    }
}
