using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Addic7ed.Model;
using Addic7ed.Model.Addicted;
using Addic7ed.Model.Addicted.Config;
using Addic7ed.Model.Addicted.Proxy;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Security;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace Addic7ed
{
    public class Addic7edDownloader : ISubtitleProvider, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;

        private readonly IServerConfigurationManager _config;
        private readonly IEncryptionManager _encryption;

        private readonly IJsonSerializer _json;
        private readonly IFileSystem _fileSystem;

        private readonly string _baseUrl = "http://localhost:5000/addic7ed";
        private readonly ILocalizationManager _localizationManager;
        private  Addic7edCreds _addic7EdCreds;

        public Addic7edDownloader(ILogger logger, IHttpClient httpClient, IServerConfigurationManager config, IEncryptionManager encryption, IJsonSerializer json, IFileSystem fileSystem, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = config;
            _encryption = encryption;
            _json = json;
            _fileSystem = fileSystem;
            _localizationManager = localizationManager;
            _config.NamedConfigurationUpdating += _config_NamedConfigurationUpdating;
            _config.NamedConfigurationUpdated += (sender, args) => SetCreds();
            SetCreds();
        }

        private void SetCreds()
        {
            var addic7EdOptions = GetOptions();
            var decryptedPass   = DecryptPassword(addic7EdOptions.Addic7edPasswordHash);
            _addic7EdCreds = new Addic7edCreds
            {
                UserId   = int.Parse(addic7EdOptions.Addic7edUsername),
                Password = decryptedPass
            };
        }

        private const string PasswordHashPrefix = "h:";
        void _config_NamedConfigurationUpdating(object sender, ConfigurationUpdateEventArgs e)
        {
            if (!string.Equals(e.Key, "addic7ed", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var options = (Addic7edOptions)e.NewConfiguration;

            if (options != null &&
                !string.IsNullOrWhiteSpace(options.Addic7edPasswordHash) &&
                !options.Addic7edPasswordHash.StartsWith(PasswordHashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                options.Addic7edPasswordHash = EncryptPassword(options.Addic7edPasswordHash);
            }
        }

        private string EncryptPassword(string password)
        {
            return PasswordHashPrefix + _encryption.EncryptString(password);
        }

        private string DecryptPassword(string password)
        {
            if (password == null ||
                !password.StartsWith(PasswordHashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return _encryption.DecryptString(password.Substring(2));
        }

        public string Name => "Addic7ed";

        private Addic7edOptions GetOptions()
        {
            return _config.GetAddic7edConfiguration();
        }

        public IEnumerable<VideoContentType> SupportedMediaTypes
        {
            get
            {
                return new[] { VideoContentType.Episode, VideoContentType.Movie };
            }
        }

        private string NormalizeLanguage(string language)
        {
            if (language != null)
            {
                var culture = _localizationManager.FindLanguageInfo(language.AsSpan());
                if (culture != null)
                {
                    return culture.ThreeLetterISOLanguageName;
                }
            }

            return language;
        }

        private Task<HttpResponseInfo> PostData<T>(string url, T data,  CancellationToken cancellationToken)
        {
            return _httpClient.Post(new HttpRequestOptions
            {
                Url = $"{_baseUrl}/{url}".Replace("//", "/"),
                CancellationToken = cancellationToken,
                AcceptHeader = "application/json",
                RequestContentType = "application/json",
                TimeoutMs = (int)TimeSpan.FromMinutes(5).TotalMilliseconds,
                RequestContent =  _json.SerializeToString(data).AsMemory()
            });
        }
        

        public async Task<IEnumerable<RemoteSubtitleInfo>> SearchEpisode(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SeriesName) || !request.ParentIndexNumber.HasValue || !request.IndexNumber.HasValue || string.IsNullOrWhiteSpace(request.Language))
                return Array.Empty<RemoteSubtitleInfo>();

            _logger.Info($"[Addic7ed] Look for Subtitle for {request.SeriesName} S{request.ParentIndexNumber.Value}E{request.IndexNumber.Value}");
            var filePath = request.MediaPath.Split(Path.PathSeparator);
            var filename = filePath[filePath.Length - 1];
            try
            {
                var response = await PostData("search", new SearchRequest
                {
                    Show        = request.SeriesName,
                    Credentials = _addic7EdCreds,
                    Episode     = request.IndexNumber.Value,
                    FileName    = filename,
                    Season      = request.ParentIndexNumber.Value
                }, cancellationToken).ConfigureAwait(false);

                var searchResult = (SearchResponse) await _json.DeserializeFromStreamAsync(response.Content, typeof(SearchResponse)).ConfigureAwait(false);

                var subSource = searchResult.Episode.Subtitles;
                if (searchResult.MatchingSubtitles.Length > 0)
                {
                    subSource = searchResult.MatchingSubtitles;
                }

                return subSource
                       .Select(subtitle => ConvertFromSubtitle(searchResult.Episode, subtitle))
                       .Where(info => info.ThreeLetterISOLanguageName == request.Language)
                       .ToArray();
               
            }
            catch (HttpException e)
            {
                if (e.StatusCode != HttpStatusCode.NotFound) 
                    throw;
                _logger.Info($"[Addic7ed] No Subtitle for {request.SeriesName} S{request.ParentIndexNumber.Value}E{request.IndexNumber.Value}");
                return Array.Empty<RemoteSubtitleInfo>();
            }
        }

        private RemoteSubtitleInfo ConvertFromSubtitle(Episode episode, Subtitle subtitle)
        {
            var threeLetterIsoLanguageName = NormalizeLanguage(subtitle.Language);
            return new RemoteSubtitleInfo
            {
                Id                         = $"{subtitle.DownloadUri.Replace("/", ",")}:{threeLetterIsoLanguageName}",
                ProviderName               = Name,
                Name                       = $"{episode.Title} - {subtitle.Version} {(subtitle.HearingImpaired ? "- Hearing Impaired" : "")}",
                Format                     = "srt",
                ThreeLetterISOLanguageName = threeLetterIsoLanguageName
            };
        }


        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            if (request.IsForced.HasValue)
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }
            if (request.ContentType.Equals(VideoContentType.Episode))
            {
                return await SearchEpisode(request, cancellationToken);
            }

            return Array.Empty<RemoteSubtitleInfo>();
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var idParts  = id.Split(new[] { ':' }, 2);
            var download = idParts[0].Replace(",", "/");
            var language = idParts[1];
            _logger.Info($"[Addic7ed] Downloading {download} for language {language}");

            using (var stream = await PostData(download, _addic7EdCreds, cancellationToken))
            {
                var ms = new MemoryStream();
                await stream.Content.CopyToAsync(ms);
                ms.Position = 0;
                return new SubtitleResponse()
                {
                    Language = language,
                    Stream   = ms,
                    Format   = "srt"
                };
            }
        }

        public void Dispose()
        {
            _config.NamedConfigurationUpdating -= _config_NamedConfigurationUpdating;
        }
    }
}
