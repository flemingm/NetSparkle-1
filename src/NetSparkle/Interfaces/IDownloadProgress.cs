using System.Net;
using System;
using NetSparkleUpdater.Events;

namespace NetSparkleUpdater.Interfaces
{
    /// <summary>
    /// Interface for UI element that shows the progress bar
    /// and a method to install and relaunch the appliction
    /// </summary>
    public interface IDownloadProgress
    {
        /// <summary>
        /// Event to fire when the download UI is complete; tells you 
        /// if the install process should happen or not
        /// </summary>
        event DownloadInstallEventHandler DownloadProcessCompleted;

        /// <summary>
        /// Enable or disable the download and install button (such as when your "Can I gracefully close the window?" function is async and you don't
        /// want your user to click the button multiple times)
        /// </summary>
        /// <param name="shouldBeEnabled">True if the button should be enabled; false otherwise</param>
        void SetDownloadAndInstallButtonEnabled(bool shouldBeEnabled);

        /// <summary>
        /// Show the UI for download progress
        /// </summary>
        /// <returns>True if download was successful; false otherwise</returns>
        void Show(bool isOnMainThread);

        /// <summary>
        /// Called when the download progress changes
        /// </summary>
        /// <param name="sender">not used.</param>
        /// <param name="e">used to resolve the progress of the download. Also contains the total size of the download (TODO: docs update)</param>
        void OnDownloadProgressChanged(object sender, ItemDownloadProgressEventArgs args);

        /// <summary>
        /// Close the UI
        /// </summary>
        void Close();

        /// <summary>
        /// Update UI to show file is downloaded and signature check result
        /// </summary>
        void FinishedDownloadingFile(bool isDownloadedFileValid);

        /// <summary>
        /// Show an error message in the download progress window if possible.
        /// </summary>
        /// <param name="errorMessage">Error message to display</param>
        /// <returns>True if message displayed; false otherwise</returns>
        bool DisplayErrorMessage(string errorMessage);
    }
}
