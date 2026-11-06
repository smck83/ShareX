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

namespace ShareX
{
    /// <summary>
    /// Result of evaluating whether an upload is authorized. Anything other than
    /// <see cref="Allowed"/> means no data may leave the device yet.
    /// </summary>
    public enum UploadAuthorization
    {
        /// <summary>Upload may proceed without prompting.</summary>
        Allowed,

        /// <summary>The user must be asked before the upload may proceed.</summary>
        ConsentRequired,

        /// <summary>Blocked by the machine or user registry policy (<see cref="SystemOptions.DisableUpload"/>).</summary>
        BlockedBySystemPolicy,

        /// <summary>Blocked by the application-wide "Disable uploads" setting.</summary>
        BlockedByDisableUpload,

        /// <summary>Blocked because the user chose to never allow uploads.</summary>
        BlockedByConsentMode
    }

    /// <summary>
    /// The action a user took in the upload confirmation dialog. <see cref="Cancel"/> is the
    /// default so that a dismissed, closed or failed dialog can never authorize an upload.
    /// </summary>
    public enum UploadConsentChoice
    {
        Cancel,
        Upload,
        NeverUpload
    }

    /// <summary>
    /// The persisted pair of settings that together decide whether uploading is permitted.
    /// </summary>
    public readonly struct UploadConsentPreference
    {
        public UploadConsentMode Mode { get; }

        /// <summary>The existing application-wide "Disable uploads" setting. Authoritative over <see cref="Mode"/>.</summary>
        public bool DisableUpload { get; }

        public UploadConsentPreference(UploadConsentMode mode, bool disableUpload)
        {
            Mode = mode;
            DisableUpload = disableUpload;
        }
    }

    /// <summary>
    /// Upload authorization rules, kept free of UI and global state so that the precedence
    /// order and the effect of every dialog choice can be unit tested.
    ///
    /// Precedence, highest first:
    ///   1. <see cref="SystemOptions.DisableUpload"/> - administrator policy (registry, read only at runtime).
    ///   2. <see cref="ApplicationConfig.DisableUpload"/> - the pre-existing application-wide switch.
    ///   3. <see cref="ApplicationConfig.UploadConsentMode"/> - the user's consent preference.
    /// A user can never widen what a higher level has already denied.
    /// </summary>
    public static class UploadConsentPolicy
    {
        /// <summary>
        /// Decides whether an upload is allowed, must be confirmed, or is blocked.
        /// </summary>
        /// <param name="systemDisableUpload">Administrator policy, from <see cref="SystemOptions.DisableUpload"/>.</param>
        /// <param name="disableUpload">The application-wide "Disable uploads" setting.</param>
        /// <param name="mode">The user's consent preference.</param>
        public static UploadAuthorization Evaluate(bool systemDisableUpload, bool disableUpload, UploadConsentMode mode)
        {
            // Administrator policy first: a user preference must never be able to bypass it.
            if (systemDisableUpload)
            {
                return UploadAuthorization.BlockedBySystemPolicy;
            }

            if (disableUpload)
            {
                return UploadAuthorization.BlockedByDisableUpload;
            }

            switch (mode)
            {
                case UploadConsentMode.AlwaysAllow:
                    return UploadAuthorization.Allowed;
                case UploadConsentMode.NeverAllow:
                    return UploadAuthorization.BlockedByConsentMode;
                case UploadConsentMode.AskEveryTime:
                default:
                    // An unrecognized value (for example a hand edited or corrupted configuration
                    // file) must not be treated as permission, so fall back to asking.
                    return UploadAuthorization.ConsentRequired;
            }
        }

        /// <summary>
        /// Evaluates only the restrictions that a user preference can never widen: the
        /// administrator policy and the application-wide "Disable uploads" switch. Never returns
        /// <see cref="UploadAuthorization.ConsentRequired"/>, so callers that must not prompt
        /// can use this to ask whether uploading is permitted at all.
        /// </summary>
        public static UploadAuthorization EvaluateRestrictions(bool systemDisableUpload, bool disableUpload)
        {
            return Evaluate(systemDisableUpload, disableUpload, UploadConsentMode.AlwaysAllow);
        }

        /// <summary>
        /// Returns whether the dialog outcome authorizes the upload that triggered it.
        /// Only an explicit Upload click does.
        /// </summary>
        public static bool IsApproved(UploadConsentChoice choice)
        {
            return choice == UploadConsentChoice.Upload;
        }

        /// <summary>
        /// Maps a dialog outcome onto the settings that should be persisted.
        ///
        ///   Upload                      - this upload only, nothing persisted.
        ///   Upload + remember           - stop asking, so AlwaysAllow.
        ///   Cancel                      - this upload only, nothing persisted, ask again next time.
        ///   Cancel + remember           - persist the denial as NeverAllow.
        ///   Never upload                - set the existing application-wide DisableUpload switch.
        ///
        /// "Never upload" deliberately reuses DisableUpload rather than also writing NeverAllow:
        /// one deny switch per level keeps the settings UI honest and lets the user re-enable
        /// uploading by clearing the one checkbox they ticked.
        /// </summary>
        public static UploadConsentPreference ApplyChoice(UploadConsentChoice choice, bool rememberChoice,
            UploadConsentPreference current)
        {
            switch (choice)
            {
                case UploadConsentChoice.NeverUpload:
                    return new UploadConsentPreference(current.Mode, true);

                case UploadConsentChoice.Upload:
                    return rememberChoice
                        ? new UploadConsentPreference(UploadConsentMode.AlwaysAllow, current.DisableUpload)
                        : current;

                case UploadConsentChoice.Cancel:
                default:
                    return rememberChoice
                        ? new UploadConsentPreference(UploadConsentMode.NeverAllow, current.DisableUpload)
                        : current;
            }
        }

        /// <summary>
        /// The consent mode an installation that predates this feature should be migrated to.
        ///
        /// An installation that was uploading keeps uploading exactly as before
        /// (<see cref="UploadConsentMode.AlwaysAllow"/>), so an upgrade never silently breaks
        /// established automation. New installations are not migrated at all and so keep the
        /// <see cref="UploadConsentMode.AskEveryTime"/> default declared on <see cref="ApplicationConfig"/>.
        ///
        /// An installation that had uploads disabled stays disabled, because DisableUpload is
        /// authoritative, and is left on the privacy-first default: it has expressed no preference
        /// about prompting, so if the user later clears DisableUpload they are asked rather than
        /// silently uploaded from.
        /// </summary>
        public static UploadConsentMode GetMigratedMode(bool disableUpload)
        {
            return disableUpload ? UploadConsentMode.AskEveryTime : UploadConsentMode.AlwaysAllow;
        }
    }
}
