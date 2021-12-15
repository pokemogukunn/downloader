﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonExtensions.Http;
using JsonExtensions.Reading;
using YoutubeDownloader.Core.Utils;
using YoutubeDownloader.Core.Utils.Extensions;

namespace YoutubeDownloader.Core.Downloading.Tagging;

internal class MusicBrainzClient
{
    // 4 requests per second
    private readonly ThrottleLock _throttleLock = new(TimeSpan.FromSeconds(1.0 / 4));

    public async Task<IReadOnlyList<MusicBrainzRecording>> FindRecordingsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var url =
            "http://musicbrainz.org/ws/2/recording/?version=2&fmt=json&dismax=true&limit=25&query=" +
            Uri.EscapeDataString(query);

        await _throttleLock.WaitAsync(cancellationToken);
        var json = await Http.Client.GetJsonAsync(url, cancellationToken);

        var recordingsJson = json.GetPropertyOrNull("recordings")?.EnumerateArrayOrNull() ?? default;
        var recordings = new List<MusicBrainzRecording>();

        foreach (var recordingJson in recordingsJson)
        {
            var artist = recordingJson
                .GetPropertyOrNull("artist-credit")?
                .EnumerateArrayOrNull()?
                .FirstOrDefault()
                .GetPropertyOrNull("name")?
                .GetNonWhiteSpaceStringOrNull();

            if (string.IsNullOrWhiteSpace(artist))
                continue;

            var artistSort = recordingJson
                .GetPropertyOrNull("artist-credit")?
                .EnumerateArrayOrNull()?
                .FirstOrDefault()
                .GetPropertyOrNull("artist")?
                .GetPropertyOrNull("sort-name")?
                .GetNonWhiteSpaceStringOrNull();

            var title = recordingJson
                .GetPropertyOrNull("title")?
                .GetNonWhiteSpaceStringOrNull();

            if (string.IsNullOrWhiteSpace(title))
                continue;

            var album = recordingJson
                .GetPropertyOrNull("releases")?
                .EnumerateArrayOrNull()?
                .FirstOrDefault()
                .GetPropertyOrNull("title")?
                .GetNonWhiteSpaceStringOrNull();

            var duration = recordingJson
                .GetPropertyOrNull("length")?
                .GetDoubleOrNull()?
                .Pipe(TimeSpan.FromMilliseconds);

            if (duration is null)
                continue;

            recordings.Add(new MusicBrainzRecording(
                artist,
                artistSort,
                title,
                album,
                duration.Value
            ));
        }

        return recordings;
    }
}