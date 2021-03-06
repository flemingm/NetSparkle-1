﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using NetSparkleUpdater.Enums;
using System.Net;

namespace NetSparkleUpdater
{
    /// <summary>
    /// Grabs release notes formatted as Markdown from the server and allows you to view them as HTML
    /// </summary>
    public class ReleaseNotesGrabber
    {
        private string _separatorTemplate;
        private string _initialHTML;

        protected SparkleUpdater _sparkle;

        /// <summary>
        /// List of supported extensions for markdown files (.md, .mkdn, .mkd, .markdown)
        /// </summary>
        public static readonly List<string> MarkdownExtensions = new List<string> { ".md", ".mkdn", ".mkd", ".markdown" };

        /// <summary>
        /// Base constructor for ReleaseNotesGrabber
        /// </summary>
        /// <param name="separatorTemplate">Template to use for separating each item in the HTML</param>
        /// <param name="htmlHeadAddition">Any additional header information to stick in the HTML that will show up in the release notes</param>
        /// <param name="sparkle">Sparkle updater being used</param>
        public ReleaseNotesGrabber(string separatorTemplate, string htmlHeadAddition, SparkleUpdater sparkle)
        {
            _separatorTemplate =
                !string.IsNullOrEmpty(separatorTemplate) ?
                    separatorTemplate :
                    "<div style=\"border: #ccc 1px solid;\"><div style=\"background: {3}; padding: 5px;\"><span style=\"float: right; display:float;\">" +
                    "{1}</span>{0}</div><div style=\"padding: 5px;\">{2}</div></div><br>";
            _initialHTML = "<html><head><meta http-equiv='Content-Type' content='text/html;charset=UTF-8'>" + htmlHeadAddition + "</head><body>";
            _sparkle = sparkle;
        }

        /// <summary>
        /// Generates the text to display while release notes are loading
        /// </summary>
        /// <returns>HTML to show to the user while release notes are loading</returns>
        public virtual string GetLoadingText()
        {
            return _initialHTML + "<p><em>Loading release notes...</em></p></body></html>"; ;
        }

        /// <summary>
        /// Download all of the release notes provided to this function and convert them to HTML
        /// </summary>
        /// <param name="items">List of items that you want to display in the release notes</param>
        /// <param name="latestVersion">The latest version (most current version) of your releases</param>
        /// <param name="cancellationToken">Token to cancel the async download requests</param>
        /// <returns></returns>
        public virtual async Task<string> DownloadAllReleaseNotes(List<AppCastItem> items, AppCastItem latestVersion, CancellationToken cancellationToken)
        {
            _sparkle.LogWriter.PrintMessage("Preparing to initialize release notes...");
            StringBuilder sb = new StringBuilder(_initialHTML);
            foreach (AppCastItem castItem in items)
            {
                _sparkle.LogWriter.PrintMessage("Initializing release notes for {0}", castItem.Version);
                // TODO: could we optimize this by doing multiple downloads at once?
                var releaseNotes = await GetReleaseNotes(castItem, _sparkle, cancellationToken);
                sb.Append(string.Format(_separatorTemplate,
                                        castItem.Version,
                                        castItem.PublicationDate.ToString("D"), // was dd MMM yyyy
                                        releaseNotes,
                                        latestVersion.Version.Equals(castItem.Version) ? "#ABFF82" : "#AFD7FF"));
            }
            sb.Append("</body>");

            _sparkle.LogWriter.PrintMessage("Done initializing release notes!");
            return sb.ToString();
        }

        protected virtual async Task<string> GetReleaseNotes(AppCastItem item, SparkleUpdater sparkle, CancellationToken cancellationToken)
        {
            string criticalUpdate = item.IsCriticalUpdate ? "Critical Update" : "";
            // at first try to use embedded description
            if (!string.IsNullOrEmpty(item.Description))
            {
                // check for markdown
                Regex containsHtmlRegex = new Regex(@"<\s*([^ >]+)[^>]*>.*?<\s*/\s*\1\s*>");
                if (containsHtmlRegex.IsMatch(item.Description))
                {
                    if (item.IsCriticalUpdate)
                    {
                        item.Description = "<p><em>" + criticalUpdate + "</em></p>" + "<br>" + item.Description;
                    }
                    return item.Description;
                }
                else
                {
                    var md = new MarkdownSharp.Markdown();
                    if (item.IsCriticalUpdate)
                    {
                        item.Description = "*" + criticalUpdate + "*" + "\n\n" + item.Description;
                    }
                    var temp = md.Transform(item.Description);
                    return temp;
                }
            }

            // not embedded so try to release notes from the link
            if (string.IsNullOrEmpty(item.ReleaseNotesLink))
            {
                return null;
            }

            // download release notes
            sparkle.LogWriter.PrintMessage("Downloading release notes for {0} at {1}", item.Version, item.ReleaseNotesLink);
            string notes = await DownloadReleaseNotes(item.ReleaseNotesLink, cancellationToken, sparkle);
            sparkle.LogWriter.PrintMessage("Done downloading release notes for {0}", item.Version);
            if (string.IsNullOrEmpty(notes))
            {
                return null;
            }

            // check dsa of release notes
            if (!string.IsNullOrEmpty(item.ReleaseNotesDSASignature))
            {
                if (sparkle.SignatureVerifier.VerifySignatureOfString(item.ReleaseNotesDSASignature, notes) == ValidationResult.Invalid)
                    return null;
            }

            // process release notes
            var extension = Path.GetExtension(item.ReleaseNotesLink);
            if (extension != null && MarkdownExtensions.Contains(extension.ToLower()))
            {
                try
                {
                    var md = new MarkdownSharp.Markdown();
                    if (item.IsCriticalUpdate)
                    {
                        notes = "*" + criticalUpdate + "*" + "\n\n" + notes;
                    }
                    notes = md.Transform(notes);
                }
                catch (Exception ex)
                {
                    sparkle.LogWriter.PrintMessage("Error parsing Markdown syntax: {0}", ex.Message);
                }
            }
            return notes;
        }

        protected virtual async Task<string> DownloadReleaseNotes(string link, CancellationToken cancellationToken, SparkleUpdater sparkle)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                    webClient.Encoding = Encoding.UTF8;
                    if (cancellationToken != null)
                    {
                        using (cancellationToken.Register(() => webClient.CancelAsync()))
                        {
                            return await webClient.DownloadStringTaskAsync(Utilities.GetAbsoluteURL(link, sparkle.AppCastUrl));
                        }
                    }
                    return await webClient.DownloadStringTaskAsync(Utilities.GetAbsoluteURL(link, sparkle.AppCastUrl));
                }
            }
            catch (WebException ex)
            {
                sparkle.LogWriter.PrintMessage("Cannot download release notes from {0} because {1}", link, ex.Message);
                return "";
            }
        }
    }
}
