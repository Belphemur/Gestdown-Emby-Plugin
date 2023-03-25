#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Gestdown.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Security;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace Gestdown
{
    class GestdownDownloader : ISubtitleProvider, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly ILibraryManager _libraryManager;
        private readonly IJsonSerializer _jsonSerializer;


        private readonly string _baseUrl = "https://api.gestdown.info";
        private readonly ILocalizationManager _localizationManager;
        private readonly Version _clientVersion;

        public GestdownDownloader(ILogger logger, IHttpClient httpClient, ILibraryManager libraryManager, IJsonSerializer jsonSerializer, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _httpClient = httpClient;
            _libraryManager = libraryManager;
            _jsonSerializer = jsonSerializer;
            _localizationManager = localizationManager;
            _clientVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? typeof(GestdownDownloader).Assembly.GetName().Version ?? new Version(1, 0, 0);
        }


        public string Name => "Gestdown";


        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode };

        private string? NormalizeLanguage(string? language)
        {
            if (language == null)
            {
                return language;
            }

            var culture = _localizationManager.FindLanguageInfo(language.AsSpan());
            if (culture != null)
            {
                return culture.TwoLetterISOLanguageName;
            }

            return language;
        }


        private async Task<T?> GetJsonResponse<T>(string url, CancellationToken cancellationToken) where T : class
        {
            try
            {
                using var response = await GetResponse(url, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                return _jsonSerializer.DeserializeFromStream<T>(response.Content);
            }
            catch (HttpException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private Task<HttpResponseInfo> GetResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                Url = $"{_baseUrl}/{url}",
                CancellationToken = cancellationToken,
                Referer = _baseUrl,
                UserAgent = "Emby/" + _clientVersion
            });
        }


        public async Task<IEnumerable<RemoteSubtitleInfo>> SearchEpisode(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new []{"Series"},
                SearchTerm = request.SeriesName
            });

            if (items == null || items.Length == 0)
            {
                _logger.Info($"Couldn't find the show in library");
                return Array.Empty<RemoteSubtitleInfo>();
            }

            var tvDbId = items.First().GetProviderId(MetadataProviders.Tvdb);
            _logger.Info($"Getting show for show tvdb: {tvDbId}");

            var language = request.Language;

            if (string.IsNullOrWhiteSpace(tvDbId) ||
                !request.ParentIndexNumber.HasValue ||
                !request.IndexNumber.HasValue ||
                string.IsNullOrWhiteSpace(language))
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }

            language = NormalizeLanguage(language);

            var showResponse = await GetJsonResponse<ShowResponse>($"shows/external/tvdb/{tvDbId}", cancellationToken).ConfigureAwait(false);
            if (showResponse == null || showResponse.shows.Count == 0)
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }


            foreach (var show in showResponse.shows)
            {
                var episodes = await GetJsonResponse<SubtitleSearchResponse>($"subtitles/get/{show.id}/{request.ParentIndexNumber}/{request.IndexNumber}/{language}", cancellationToken).ConfigureAwait(false);
                if (episodes == null || episodes.matchingSubtitles.Count == 0)
                {
                    continue;
                }

                return episodes.matchingSubtitles.Select(subtitle => new RemoteSubtitleInfo
                {
                    Id = $"{subtitle.downloadUri.Substring(1).Replace("/", ",")}:{subtitle.language}",
                    ProviderName = Name,
                    Name = $"{subtitle.version}{(subtitle.hearingImpaired ? "- Hearing Impaired" : "")}",
                    Format = "srt",
                    Language = subtitle.language,
                    DownloadCount = subtitle.downloadCount
                });
            }

            return Array.Empty<RemoteSubtitleInfo>();
        }

        public Task<IEnumerable<RemoteSubtitleInfo>> SearchMovie(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<RemoteSubtitleInfo>>(Array.Empty<RemoteSubtitleInfo>());
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            if (request.IsForced.HasValue)
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }

            if (request.ContentType.Equals(VideoContentType.Episode))
            {
                return await SearchEpisode(request, cancellationToken).ConfigureAwait(false);
            }

            if (request.ContentType.Equals(VideoContentType.Movie))
            {
                return await SearchMovie(request, cancellationToken).ConfigureAwait(false);
            }

            return Array.Empty<RemoteSubtitleInfo>();
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var idParts = id.Split(new[] { ':' }, 2);
            var download = idParts[0].Replace(",", "/");
            var language = idParts[1];
            var format = "srt";
            
            using var stream = await GetResponse(download, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stream.ContentType) &&
                !stream.ContentType.Contains(format))
            {
                return new SubtitleResponse();
            }

            var ms = new MemoryStream();
            await stream.Content.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            return new SubtitleResponse()
            {
                Language = language,
                Stream = ms,
                Format = format
            };
        }

        public void Dispose()
        {
        }
    }
}