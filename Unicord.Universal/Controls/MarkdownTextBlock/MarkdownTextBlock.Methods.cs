// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ColorCode;
using Unicord;
using Unicord.Universal;
using WamWooWam.Parsers.Markdown;
using WamWooWam.Uwp.UI.Controls.Markdown.Render;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace WamWooWam.Uwp.UI.Controls
{
    /// <summary>
    /// An efficient and extensible control that can parse and render markdown.
    /// </summary>
    public partial class MarkdownTextBlock
    {
        /// <summary>
        /// Sets the Markdown Renderer for Rendering the UI.
        /// </summary>
        /// <typeparam name="T">The Inherited Markdown Render</typeparam>
        public void SetRenderer<T>()
            where T : MarkdownRenderer
        {
            renderertype = typeof(T);
        }

        /// <summary>
        /// Called to preform a render of the current Markdown.
        /// </summary>
        private void RenderMarkdown()
        {
            // Leave if we don't have our root yet.
            if (_rootElement == null)
            {
                return;
            }

            // Disconnect from OnClick handlers.
            UnhookListeners();

            var markdownRenderedArgs = new MarkdownRenderedEventArgs(null);

            // Make sure we have something to parse.
            if (string.IsNullOrWhiteSpace(Text))
            {
                _rootElement.Child = null;
            }
            else
            {
                try
                {
                    // Try to parse the markdown.
                    var markdown = new MarkdownDocument();
                    foreach (var str in SchemeList.Split(',').ToList())
                    {
                        if (!string.IsNullOrEmpty(str))
                        {
                            MarkdownDocument.KnownSchemes.Add(str);
                        }
                    }

                    markdown.Parse(Text);

                    // Now try to display it
                    var renderer = Activator.CreateInstance(renderertype, markdown, this, this, this) as MarkdownRenderer;
                    if (renderer == null)
                    {
                        throw new Exception("Markdown Renderer was not of the correct type.");
                    }

                    renderer.Background = Background;
                    renderer.BorderBrush = BorderBrush;
                    renderer.BorderThickness = BorderThickness;
                    renderer.CharacterSpacing = CharacterSpacing;
                    renderer.FontFamily = FontFamily;
                    renderer.FontSize = FontSize;
                    renderer.FontStretch = FontStretch;
                    renderer.FontStyle = FontStyle;
                    renderer.FontWeight = FontWeight;
                    renderer.Foreground = Foreground;
                    renderer.IsTextSelectionEnabled = IsTextSelectionEnabled;
                    renderer.Padding = Padding;
                    renderer.CodeBackground = CodeBackground;
                    renderer.CodeBorderBrush = CodeBorderBrush;
                    renderer.CodeBorderThickness = CodeBorderThickness;
                    renderer.InlineCodeBorderThickness = InlineCodeBorderThickness;
                    renderer.InlineCodeBackground = InlineCodeBackground;
                    renderer.InlineCodeBorderBrush = InlineCodeBorderBrush;
                    renderer.InlineCodePadding = InlineCodePadding;
                    renderer.InlineCodeFontFamily = InlineCodeFontFamily;
                    renderer.CodeForeground = CodeForeground;
                    renderer.CodeFontFamily = CodeFontFamily;
                    renderer.CodePadding = CodePadding;
                    renderer.CodeMargin = CodeMargin;
                    renderer.EmojiFontFamily = EmojiFontFamily;
                    renderer.Header1FontSize = Header1FontSize;
                    renderer.Header1FontWeight = Header1FontWeight;
                    renderer.Header1Margin = Header1Margin;
                    renderer.Header1Foreground = Header1Foreground;
                    renderer.Header2FontSize = Header2FontSize;
                    renderer.Header2FontWeight = Header2FontWeight;
                    renderer.Header2Margin = Header2Margin;
                    renderer.Header2Foreground = Header2Foreground;
                    renderer.Header3FontSize = Header3FontSize;
                    renderer.Header3FontWeight = Header3FontWeight;
                    renderer.Header3Margin = Header3Margin;
                    renderer.Header3Foreground = Header3Foreground;
                    renderer.Header4FontSize = Header4FontSize;
                    renderer.Header4FontWeight = Header4FontWeight;
                    renderer.Header4Margin = Header4Margin;
                    renderer.Header4Foreground = Header4Foreground;
                    renderer.Header5FontSize = Header5FontSize;
                    renderer.Header5FontWeight = Header5FontWeight;
                    renderer.Header5Margin = Header5Margin;
                    renderer.Header5Foreground = Header5Foreground;
                    renderer.Header6FontSize = Header6FontSize;
                    renderer.Header6FontWeight = Header6FontWeight;
                    renderer.Header6Margin = Header6Margin;
                    renderer.Header6Foreground = Header6Foreground;
                    renderer.HorizontalRuleBrush = HorizontalRuleBrush;
                    renderer.HorizontalRuleMargin = HorizontalRuleMargin;
                    renderer.HorizontalRuleThickness = HorizontalRuleThickness;
                    renderer.ListMargin = ListMargin;
                    renderer.ListGutterWidth = ListGutterWidth;
                    renderer.ListBulletSpacing = ListBulletSpacing;
                    renderer.ParagraphMargin = ParagraphMargin;
                    renderer.QuoteBackground = QuoteBackground;
                    renderer.QuoteBorderBrush = QuoteBorderBrush;
                    renderer.QuoteBorderThickness = QuoteBorderThickness;
                    renderer.QuoteForeground = QuoteForeground;
                    renderer.QuoteMargin = QuoteMargin;
                    renderer.QuotePadding = QuotePadding;
                    renderer.TableBorderBrush = TableBorderBrush;
                    renderer.TableBorderThickness = TableBorderThickness;
                    renderer.TableCellPadding = TableCellPadding;
                    renderer.TableMargin = TableMargin;
                    renderer.TextWrapping = TextWrapping;
                    renderer.LinkForeground = LinkForeground;
                    renderer.ImageStretch = ImageStretch;
                    renderer.ImageMaxHeight = ImageMaxHeight;
                    renderer.ImageMaxWidth = ImageMaxWidth;
                    renderer.WrapCodeBlock = WrapCodeBlock;
                    renderer.Channel = Channel;

                    _rootElement.Child = renderer.Render();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error while parsing and rendering: " + ex.Message);
                    Debug.WriteLine(Text);

                    if (Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }

                    markdownRenderedArgs = new MarkdownRenderedEventArgs(ex);
                }
            }

            // Indicate that the parse is done.
            MarkdownRendered?.Invoke(this, markdownRenderedArgs);
        }

        private void UnhookListeners()
        {
            // Clear any hyper link events if we have any
            foreach (var link in _listeningHyperlinks)
            {
                if (link is Hyperlink hyperlink)
                {
                    hyperlink.Click -= Hyperlink_Click;
                }
                else if (link is Image image)
                {
                    image.Tapped -= NewImagelink_Tapped;
                }
            }

            // Clear everything that exists.
            _listeningHyperlinks.Clear();
        }

        /// <summary>
        /// Called when the render has a link we need to listen to.
        /// </summary>
        public void RegisterNewHyperLink(Hyperlink newHyperlink, string linkUrl)
        {
            // Setup a listener for clicks.
            newHyperlink.Click += Hyperlink_Click;

            // Associate the URL with the hyperlink.
            newHyperlink.SetValue(HyperlinkUrlProperty, linkUrl);

            // Add it to our list
            _listeningHyperlinks.Add(newHyperlink);
        }

        /// <summary>
        /// Called when the render has a link we need to listen to.
        /// </summary>
        public void RegisterNewHyperLink(Image newImagelink, string linkUrl, bool isHyperLink)
        {
            // Setup a listener for clicks.
            newImagelink.Tapped += NewImagelink_Tapped;

            // Associate the URL with the hyperlink.
            newImagelink.SetValue(HyperlinkUrlProperty, linkUrl);

            // Set if the Image is HyperLink or not
            newImagelink.SetValue(IsHyperlinkProperty, isHyperLink);

            // Add it to our list
            _listeningHyperlinks.Add(newImagelink);
        }

        /// <summary>
        /// Called when the renderer needs to display a image.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        async Task<ImageSource> IImageResolver.ResolveImageAsync(string url, string tooltip)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                if (!string.IsNullOrEmpty(UriPrefix))
                {
                    url = string.Format("{0}{1}", UriPrefix, url);
                }
            }

            var eventArgs = new ImageResolvingEventArgs(url, tooltip);
            ImageResolving?.Invoke(this, eventArgs);

            await eventArgs.WaitForDeferrals();

            try
            {
                return eventArgs.Handled
                    ? eventArgs.Image
                    : GetImageSource(new Uri(url));
            }
            catch (Exception)
            {
                return null;
            }

            ImageSource GetImageSource(Uri imageUrl)
            {
                if (_isSvgImageSupported)
                {
                    if (Path.GetExtension(imageUrl.AbsolutePath)?.ToLowerInvariant() == ".svg")
                    {
                        return new SvgImageSource(imageUrl);
                    }
                }

                return new BitmapImage(imageUrl);
            }
        }

        /// <summary>
        /// Called when a Code Block is being rendered.
        /// </summary>
        /// <returns>Parsing was handled Successfully</returns>
        bool ICodeBlockResolver.ParseSyntax(InlineCollection inlineCollection, string text, string codeLanguage)
        {
            var eventArgs = new CodeBlockResolvingEventArgs(inlineCollection, text, codeLanguage);
            CodeBlockResolving?.Invoke(this, eventArgs);

            try
            {
                var result = eventArgs.Handled;
                if (UseSyntaxHighlighting && !result && codeLanguage != null)
                {
                    var language = Languages.FindById(codeLanguage);
                    if (language != null)
                    {
                        RichTextBlockFormatter formatter;
                        if (CodeStyling != null)
                        {
                            formatter = new RichTextBlockFormatter(CodeStyling);
                        }
                        else
                        {
                            var theme = themeListener.CurrentTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                            if (RequestedTheme != ElementTheme.Default)
                            {
                                theme = RequestedTheme;
                            }

                            formatter = new RichTextBlockFormatter(theme);
                        }

                        formatter.FormatInlines(text, language, inlineCollection);
                        return true;
                    }
                }

                return result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called when a link needs to be handled
        /// </summary>
        internal async void LinkHandled(string url, bool isHyperlink)
        {
            // Links that are nested within superscript elements cause the Click event to fire multiple times.
            // e.g. this markdown "[^bot](http://www.reddit.com/r/youtubefactsbot/wiki/index)"
            // Therefore we detect and ignore multiple clicks.
            if (multiClickDetectionTriggered)
            {
                return;
            }

            multiClickDetectionTriggered = true;
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () => multiClickDetectionTriggered = false);

            // Get the hyperlink URL.
            if (url == null)
            {
                return;
            }

            // Fire off the event.
            //var eventArgs = new LinkClickedEventArgs(url);
            //if (isHyperlink)
            //{
            //    LinkClicked?.Invoke(this, eventArgs);
            //}
            //else
            //{
            //    ImageClicked?.Invoke(this, eventArgs);
            //}

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                await Launcher.LaunchUriAsync(uri, new LauncherOptions() { TreatAsUntrusted = App.RoamingSettings.Read(Constants.WARN_UNSAFE_LINKS, true) && uri.Scheme != Uri.UriSchemeHttps });
            }
        }
    }
}