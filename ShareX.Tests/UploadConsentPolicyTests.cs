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

using System;
using System.Collections.Generic;
using Xunit;

namespace ShareX.Tests
{
    /// <summary>
    /// Covers the upload authorization decision. No test here performs or mocks a real upload:
    /// the point is that <see cref="UploadConsentPolicy"/> is the only thing that decides whether
    /// <see cref="WorkerTask"/> is allowed to reach its uploaders at all, so proving the decision
    /// is correct proves no upload is started without authorization.
    /// </summary>
    public class UploadConsentPolicyTests
    {
        // Scenario 1: asking is the behaviour when the user has not decided.
        [Fact]
        public void Evaluate_AskEveryTime_RequiresConsent()
        {
            Assert.Equal(UploadAuthorization.ConsentRequired,
                UploadConsentPolicy.Evaluate(false, false, UploadConsentMode.AskEveryTime));
        }

        // Scenario 8: changing the preference changes subsequent behaviour.
        [Fact]
        public void Evaluate_AlwaysAllow_AllowsWithoutPrompting()
        {
            Assert.Equal(UploadAuthorization.Allowed,
                UploadConsentPolicy.Evaluate(false, false, UploadConsentMode.AlwaysAllow));
        }

        // Scenario 7: a remembered "never" keeps blocking later uploads.
        [Fact]
        public void Evaluate_NeverAllow_Blocks()
        {
            Assert.Equal(UploadAuthorization.BlockedByConsentMode,
                UploadConsentPolicy.Evaluate(false, false, UploadConsentMode.NeverAllow));
        }

        // Scenario 9: DisableUpload wins over every consent mode.
        [Theory]
        [InlineData(UploadConsentMode.AskEveryTime)]
        [InlineData(UploadConsentMode.AlwaysAllow)]
        [InlineData(UploadConsentMode.NeverAllow)]
        public void Evaluate_DisableUpload_BlocksRegardlessOfConsentMode(UploadConsentMode mode)
        {
            Assert.Equal(UploadAuthorization.BlockedByDisableUpload,
                UploadConsentPolicy.Evaluate(false, true, mode));
        }

        // Scenario 10: a user preference can never override the administrator policy.
        [Theory]
        [InlineData(UploadConsentMode.AskEveryTime)]
        [InlineData(UploadConsentMode.AlwaysAllow)]
        [InlineData(UploadConsentMode.NeverAllow)]
        public void Evaluate_SystemPolicy_BlocksRegardlessOfConsentMode(UploadConsentMode mode)
        {
            Assert.Equal(UploadAuthorization.BlockedBySystemPolicy,
                UploadConsentPolicy.Evaluate(true, false, mode));
        }

        [Fact]
        public void Evaluate_SystemPolicy_TakesPrecedenceOverDisableUpload()
        {
            // The reported reason matters: the user is told the administrator disabled uploading,
            // not that they can turn it back on in settings.
            Assert.Equal(UploadAuthorization.BlockedBySystemPolicy,
                UploadConsentPolicy.Evaluate(true, true, UploadConsentMode.AlwaysAllow));
        }

        // An unrecognized persisted value, for example from a hand edited configuration file,
        // must not be read as permission.
        [Fact]
        public void Evaluate_UnknownConsentMode_RequiresConsent()
        {
            Assert.Equal(UploadAuthorization.ConsentRequired,
                UploadConsentPolicy.Evaluate(false, false, (UploadConsentMode)int.MaxValue));
        }

        /// <summary>
        /// Exhausts every combination of the three inputs and asserts that exactly one of them
        /// uploads without asking: no restriction in force and the user having chosen AlwaysAllow.
        /// </summary>
        [Fact]
        public void Evaluate_AllowsSilentUploadOnlyForOneInputCombination()
        {
            List<string> silentlyAllowed = new();

            foreach (bool systemDisableUpload in new[] { false, true })
            {
                foreach (bool disableUpload in new[] { false, true })
                {
                    foreach (UploadConsentMode mode in Enum.GetValues<UploadConsentMode>())
                    {
                        if (UploadConsentPolicy.Evaluate(systemDisableUpload, disableUpload, mode) ==
                            UploadAuthorization.Allowed)
                        {
                            silentlyAllowed.Add($"system={systemDisableUpload}, disable={disableUpload}, mode={mode}");
                        }
                    }
                }
            }

            Assert.Equal(new[] { "system=False, disable=False, mode=AlwaysAllow" }, silentlyAllowed);
        }

        // Scenario 2: approving uploads the current file.
        [Fact]
        public void IsApproved_Upload_IsApproved()
        {
            Assert.True(UploadConsentPolicy.IsApproved(UploadConsentChoice.Upload));
        }

        // Scenarios 3 and 4: cancelling, and dismissing the dialog, both prevent the upload.
        [Fact]
        public void IsApproved_Cancel_IsNotApproved()
        {
            Assert.False(UploadConsentPolicy.IsApproved(UploadConsentChoice.Cancel));
        }

        [Fact]
        public void IsApproved_NeverUpload_IsNotApproved()
        {
            Assert.False(UploadConsentPolicy.IsApproved(UploadConsentChoice.NeverUpload));
        }

        /// <summary>
        /// Scenarios 4, 19 and 20. A dismissed dialog, an unavailable UI and a dialog that threw
        /// all fall back to the default <see cref="UploadConsentChoice"/>, so that default must be
        /// the declining one.
        /// </summary>
        [Fact]
        public void DefaultChoice_IsCancel_SoFailuresNeverApprove()
        {
            Assert.Equal(UploadConsentChoice.Cancel, default(UploadConsentChoice));
            Assert.False(UploadConsentPolicy.IsApproved(default));
        }

        // Scenario: approving once does not change the stored preference, so the next upload asks again.
        [Fact]
        public void ApplyChoice_UploadWithoutRemember_DoesNotChangeSettings()
        {
            UploadConsentPreference current = new(UploadConsentMode.AskEveryTime, false);

            UploadConsentPreference updated = UploadConsentPolicy.ApplyChoice(UploadConsentChoice.Upload, false, current);

            Assert.Equal(UploadConsentMode.AskEveryTime, updated.Mode);
            Assert.False(updated.DisableUpload);
        }

        // Scenario 5: Upload + don't ask again persists AlwaysAllow.
        [Fact]
        public void ApplyChoice_UploadWithRemember_PersistsAlwaysAllow()
        {
            UploadConsentPreference current = new(UploadConsentMode.AskEveryTime, false);

            UploadConsentPreference updated = UploadConsentPolicy.ApplyChoice(UploadConsentChoice.Upload, true, current);

            Assert.Equal(UploadConsentMode.AlwaysAllow, updated.Mode);
            Assert.False(updated.DisableUpload);
        }

        // Scenario 3: cancelling once leaves the preference alone, so the next upload asks again.
        [Fact]
        public void ApplyChoice_CancelWithoutRemember_DoesNotChangeSettings()
        {
            UploadConsentPreference current = new(UploadConsentMode.AskEveryTime, false);

            UploadConsentPreference updated = UploadConsentPolicy.ApplyChoice(UploadConsentChoice.Cancel, false, current);

            Assert.Equal(UploadConsentMode.AskEveryTime, updated.Mode);
            Assert.False(updated.DisableUpload);
        }

        // Scenario 6: Cancel + don't ask again persists the denial.
        [Fact]
        public void ApplyChoice_CancelWithRemember_PersistsNeverAllow()
        {
            UploadConsentPreference current = new(UploadConsentMode.AskEveryTime, false);

            UploadConsentPreference updated = UploadConsentPolicy.ApplyChoice(UploadConsentChoice.Cancel, true, current);

            Assert.Equal(UploadConsentMode.NeverAllow, updated.Mode);
            Assert.False(updated.DisableUpload);

            // And that denial then blocks the next upload.
            Assert.Equal(UploadAuthorization.BlockedByConsentMode,
                UploadConsentPolicy.Evaluate(false, updated.DisableUpload, updated.Mode));
        }

        /// <summary>
        /// Scenario 7. "Never upload" sets the existing application-wide switch, which is what
        /// disables uploading globally, and that switch then blocks the next upload.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyChoice_NeverUpload_DisablesUploadingGlobally(bool rememberChoice)
        {
            UploadConsentPreference current = new(UploadConsentMode.AskEveryTime, false);

            UploadConsentPreference updated = UploadConsentPolicy.ApplyChoice(UploadConsentChoice.NeverUpload,
                rememberChoice, current);

            Assert.True(updated.DisableUpload);

            Assert.Equal(UploadAuthorization.BlockedByDisableUpload,
                UploadConsentPolicy.Evaluate(false, updated.DisableUpload, updated.Mode));
        }

        [Fact]
        public void ApplyChoice_DoesNotClearDisableUpload()
        {
            // Nothing a user can choose in the dialog may re-enable uploading, because the
            // dialog is only ever reached while uploading is already permitted.
            UploadConsentPreference current = new(UploadConsentMode.AskEveryTime, true);

            foreach (UploadConsentChoice choice in Enum.GetValues<UploadConsentChoice>())
            {
                Assert.True(UploadConsentPolicy.ApplyChoice(choice, false, current).DisableUpload);
                Assert.True(UploadConsentPolicy.ApplyChoice(choice, true, current).DisableUpload);
            }
        }

        /// <summary>
        /// Scenario 11, and the migration decision: an installation that was uploading before this
        /// feature existed keeps uploading without prompts, so upgrading does not interrupt
        /// established automation.
        /// </summary>
        [Fact]
        public void GetMigratedMode_UploadingInstallation_KeepsUploadingWithoutPrompting()
        {
            Assert.Equal(UploadConsentMode.AlwaysAllow, UploadConsentPolicy.GetMigratedMode(false));
        }

        /// <summary>
        /// Scenario 11. An installation that already had uploads disabled stays disabled, and is
        /// left on the privacy-first default so that clearing DisableUpload later asks rather
        /// than silently uploads.
        /// </summary>
        [Fact]
        public void GetMigratedMode_DisabledInstallation_StaysDisabledAndAsksIfReEnabled()
        {
            UploadConsentMode migrated = UploadConsentPolicy.GetMigratedMode(true);

            Assert.Equal(UploadConsentMode.AskEveryTime, migrated);

            // Still disabled immediately after the upgrade.
            Assert.Equal(UploadAuthorization.BlockedByDisableUpload,
                UploadConsentPolicy.Evaluate(false, true, migrated));

            // And if the user clears DisableUpload, they are asked rather than uploaded from.
            Assert.Equal(UploadAuthorization.ConsentRequired,
                UploadConsentPolicy.Evaluate(false, false, migrated));
        }

        [Fact]
        public void GetMigratedMode_NeverProducesASilentlyUploadingDisabledInstallation()
        {
            Assert.NotEqual(UploadConsentMode.AlwaysAllow, UploadConsentPolicy.GetMigratedMode(true));
        }

        [Fact]
        public void EvaluateRestrictions_NeverRequiresConsent()
        {
            // Callers that must not prompt, such as menu state, use this overload.
            Assert.Equal(UploadAuthorization.Allowed, UploadConsentPolicy.EvaluateRestrictions(false, false));
            Assert.Equal(UploadAuthorization.BlockedByDisableUpload, UploadConsentPolicy.EvaluateRestrictions(false, true));
            Assert.Equal(UploadAuthorization.BlockedBySystemPolicy, UploadConsentPolicy.EvaluateRestrictions(true, false));
        }

        /// <summary>
        /// Scenarios 21 and 22. The decision is a pure function of the persisted settings, so a
        /// retry or a second concurrent task re-evaluating it can only reach the same conclusion;
        /// it cannot accumulate permission from a previous attempt.
        /// </summary>
        [Fact]
        public void Evaluate_IsPureSoRetriesAndConcurrentTasksCannotAccumulatePermission()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.Equal(UploadAuthorization.ConsentRequired,
                    UploadConsentPolicy.Evaluate(false, false, UploadConsentMode.AskEveryTime));
                Assert.Equal(UploadAuthorization.BlockedByConsentMode,
                    UploadConsentPolicy.Evaluate(false, false, UploadConsentMode.NeverAllow));
            }
        }
    }
}
