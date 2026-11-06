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

using Avalonia;
using Avalonia.Threading;
using ShareX.HelpersLib;
using System;
using System.Threading.Tasks;

namespace ShareX;

internal sealed record UploadConsentResult(UploadConsentChoice Choice, bool RememberChoice)
{
    /// <summary>
    /// The result used whenever no decision could be obtained from the user, for example
    /// because no UI is available or the dialog could not be created. Never approves.
    /// </summary>
    public static UploadConsentResult Declined { get; } = new(UploadConsentChoice.Cancel, false);
}

internal static class UploadConsentWindowIntegration
{
    /// <summary>
    /// Asks the user whether to upload. Returns <see cref="UploadConsentResult.Declined"/> if the
    /// decision cannot be obtained, so an unattended or headless session fails closed.
    /// </summary>
    /// <param name="fileName">The file about to be uploaded, shown to the user.</param>
    /// <param name="destination">The destination service, shown to the user.</param>
    public static UploadConsentResult Show(string? fileName, string? destination)
    {
        // No Avalonia application means there is no interactive session to prompt in, which
        // is the unattended case: refuse rather than upload without authorization.
        if (Application.Current == null)
        {
            DebugHelper.WriteLine("Upload cancelled: upload confirmation is required but no user interface is available.");

            return UploadConsentResult.Declined;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UploadConsentResult result = UploadConsentResult.Declined;
            DispatcherFrame frame = new();
            ShowCore(fileName, destination, value =>
            {
                result = value;
                frame.Continue = false;
            });
            Dispatcher.UIThread.PushFrame(frame);
            return result;
        }

        TaskCompletionSource<UploadConsentResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() => ShowCore(fileName, destination, value => completion.TrySetResult(value)));
        return completion.Task.GetAwaiter().GetResult();
    }

    private static void ShowCore(string? fileName, string? destination, Action<UploadConsentResult> completed)
    {
        try
        {
            UploadConsentWindow window = new(fileName, destination);
            window.Closed += (_, _) => completed(new UploadConsentResult(window.Choice, window.RememberChoice));
            window.Show();
        }
        catch (Exception exception)
        {
            // A dialog that could not be shown must not be read as approval.
            DebugHelper.WriteException(exception);
            completed(UploadConsentResult.Declined);
        }
    }
}
