﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK.Data;
using PlayniteSounds.ViewModels;

namespace PlayniteSounds.Downloaders
{
    public class YoutubeItem
    {

        public string Id { get; set; }
        public string Title { get; set; }
        public Uri ThumbnailUrl { get; set; }
        public TimeSpan Duration { get; set; }
        public int Index { get; set; }
        public uint Count { get; set; }
    }

    public class YoutubeClient
    {
        private readonly HttpClient _httpClient;

        List<YoutubeItem> _items = new List<YoutubeItem>();
        public List<YoutubeItem> Results { get => _items; }

        private const string youtubeItemsListPathPlaylist = "..playlistRenderer";
        private const string youtubeContinuationTokenPath = "..continuationCommand.token";
        private const string youtubeVisitorDataPath = "..visitorData";
        private const string youtubeplaylistVideoPath = "..playlistPanelVideoRenderer";

        private const string searchTypePlaylist = "EgIQAw%3D%3D";

        public YoutubeClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        #region Search Album

        private string continuationToken = null;

        public List<YoutubeItem> Search(string searchQuery, int maxNumber = 20, CancellationToken cancellationToken = default)
        {
            continuationToken = null;
            _items = new List<YoutubeItem>();
            try
            {
                do
                {
                    var result = GetSearchResponseAsync(searchQuery, searchTypePlaylist, continuationToken, cancellationToken).Result;
                    ParseSearchResult(result);
                    MusicSelectionViewModel.SetProgress(Results.Count(), maxNumber);
                } while (continuationToken != null && continuationToken != "" && Results.Count() < maxNumber);
            }
            catch (Exception e)
            {
                PlayniteSounds.HandleException(e, cancellationToken);
            }
            return Results;
        }

        private async Task<string> GetSearchResponseAsync(
            string searchQuery,
            string searchFilter,
            string continuationToken = null,
            CancellationToken cancellationToken = default
        )
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://www.youtube.com/youtubei/v1/search"
            );

            var content = $@"{{ " +
                    (continuationToken == null
                        ? $@" ""query"": ""{WebUtility.UrlEncode(searchQuery)}"",
                        ""params"": ""{searchFilter}"","
                        : $@"""continuation"": ""{continuationToken}"","
                    ) +
                    $@" ""context"": {{
                    ""client"": {{
                        ""clientName"": ""WEB"",
                        ""clientVersion"": ""2.20210408.08.00"",
                        ""hl"": ""en"",
                        ""gl"": ""US"",
                        ""utcOffsetMinutes"": 0
                    }}
                }}
            }}";

            request.Content = new StringContent(content);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private void ParseSearchResult(string json)
        {
            dynamic jo = Serialization.FromJson<dynamic>(json);
            var playlists = new List<dynamic>(jo.SelectTokens(youtubeItemsListPathPlaylist));

            foreach (var x in playlists) {
                _items.Add(new YoutubeItem
                {
                    Id = x.SelectToken("playlistId")?.ToString(),
                    Title = x.SelectToken("title.simpleText")?.ToString(),
                    ThumbnailUrl = new Uri(x.SelectToken("thumbnails[0].thumbnails[0].url")?.ToString()),
                    Count = uint.Parse(x.SelectToken("videoCount").ToString())
                });
            }

            continuationToken = jo.SelectToken(youtubeContinuationTokenPath)?.ToString();
        }

        #endregion

        #region List Album songs
        private HashSet<string> encounteredIds = new HashSet<string>();
        private string lastVideoId = null;
        private int lastVideoIndex = 0;
        private string visitorData = null;

        public List<YoutubeItem> GetPlaylist(string playlistId, CancellationToken cancellationToken = default)
        {
            _items = new List<YoutubeItem>();
            var encounteredIds = new HashSet<string>();
            lastVideoId = null;
            lastVideoIndex = 0;
            visitorData = null;
            string result;
            try
            {
                do
                {
                    result = GetPlaylistNextResponseAsync(playlistId, lastVideoId, lastVideoIndex, visitorData, cancellationToken).Result;

                } while (ParsePlaylist(result) > 0);
            }
            catch (Exception e)
            {
                if (!cancellationToken.IsCancellationRequested)
                    PlayniteSounds.HandleException(e);
            }

            return Results;
        }

        private async Task<string> GetPlaylistNextResponseAsync(
            string playlistId,
            string videoId = null,
            int index = 0,
            string visitorData = null,
            CancellationToken cancellationToken = default
        )
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://www.youtube.com/youtubei/v1/next"
            );
            request.Content = new StringContent(
                    $@"
                {{
                    ""playlistId"": ""{playlistId}"",
                    ""videoId"": ""{videoId}"",
                    ""playlistIndex"": {index},
                    ""context"": {{
                        ""client"": {{
                            ""clientName"": ""WEB"",
                            ""clientVersion"": ""2.20210408.08.00"",
                            ""hl"": ""en"",
                            ""gl"": ""US"",
                            ""utcOffsetMinutes"": 0,
                            ""visitorData"": ""{visitorData}""
                        }}
                    }}
                }}
                "
                );

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private int ParsePlaylist(string json)
        {
            dynamic jo = Serialization.FromJson<dynamic>(json);

            visitorData = jo.SelectToken(youtubeVisitorDataPath)?.ToString();
            var videos = new List<dynamic>(jo.SelectTokens(youtubeplaylistVideoPath));

            var newItems = 0;
            foreach (var x in videos)
            {
                var item = new YoutubeItem
                {
                    Id = x.SelectToken("videoId")?.ToString(),
                    Title = x.SelectToken("title.simpleText")?.ToString(),
                    ThumbnailUrl = new Uri(x.SelectToken("thumbnail.thumbnails[0].url")?.ToString()),
                    Index = (int)x.SelectToken("navigationEndpoint.watchEndpoint.index")
                };

                if (TimeSpan.TryParseExact(
                    x.SelectToken("lengthText.simpleText")?.ToString(),
                    new string[] { "m\\:s", "h\\:m\\:\\s"},
                    null,
                    out TimeSpan duration))
                {
                    item.Duration = duration;
                }
                else
                {
                    item.Duration = TimeSpan.Parse("00:00");
                }

                if (!encounteredIds.Add(item.Id))
                    continue;
                newItems++;
                _items.Add(item);
                lastVideoId = item.Id;
                lastVideoIndex = item.Index;
            }
            return newItems;
        }

        #endregion

    }
}
