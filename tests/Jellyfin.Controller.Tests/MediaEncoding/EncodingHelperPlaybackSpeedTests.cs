using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Jellyfin.Controller.Tests.MediaEncoding;

public class EncodingHelperPlaybackSpeedTests
{
    private const long OneMinuteTicks = 600_000_000L;

    [Theory]
    [InlineData(null, null)]
    [InlineData(1.0, null)]
    [InlineData(1.0004, null)]
    [InlineData(double.NaN, null)]
    [InlineData(double.PositiveInfinity, null)]
    [InlineData(1.5, 1.5)]
    [InlineData(2.0, 2.0)]
    [InlineData(0.75, 0.75)]
    [InlineData(0.1, 0.5)]
    [InlineData(10.0, 4.0)]
    public void NormalizePlaybackSpeed_ValidatesAndClamps(double? requested, double? expected)
    {
        Assert.Equal(expected, EncodingJobInfo.NormalizePlaybackSpeed(requested));
    }

    [Fact]
    public void StreamTimeline_ScalesBySpeed()
    {
        var state = BuildState(playbackSpeed: 2.0);
        state.RunTimeTicks = 30 * OneMinuteTicks;

        Assert.Equal(15 * OneMinuteTicks, state.StreamRunTimeTicks);
        Assert.Equal(10 * OneMinuteTicks, state.StreamTicksToMediaTicks(5 * OneMinuteTicks));
    }

    [Fact]
    public void StreamTimeline_NormalSpeed_IsUnchanged()
    {
        var state = BuildState(playbackSpeed: null);
        state.RunTimeTicks = 30 * OneMinuteTicks;

        Assert.Equal(30 * OneMinuteTicks, state.StreamRunTimeTicks);
        Assert.Equal(5 * OneMinuteTicks, state.StreamTicksToMediaTicks(5 * OneMinuteTicks));
    }

    [Fact]
    public void GetAudioFilterParam_WithSpeed_RescalesTimestampsBeforeTempo()
    {
        // asetpts must come first so audio stays on the -copyts stream timeline after a seek.
        var args = CreateHelper().GetAudioFilterParam(BuildState(playbackSpeed: 2.0), new EncodingOptions());

        Assert.Contains("asetpts=PTS/2,atempo=2", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetAudioFilterParam_NormalSpeed_HasNoTempo()
    {
        var args = CreateHelper().GetAudioFilterParam(BuildState(playbackSpeed: 1.0), new EncodingOptions());

        Assert.DoesNotContain("atempo", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetVideoProcessingFilterParam_WithSpeed_AppendsSetptsAndSourceFps()
    {
        var state = BuildState(playbackSpeed: 1.5);
        var args = CreateHelper().GetVideoProcessingFilterParam(state, new EncodingOptions(), "libx264");

        Assert.EndsWith("setpts=PTS/1.5,fps=23.976\"", args, StringComparison.Ordinal);
    }

    [Fact]
    public void GetVideoProcessingFilterParam_NormalSpeed_HasNoSetpts()
    {
        var state = BuildState(playbackSpeed: null);
        var args = CreateHelper().GetVideoProcessingFilterParam(state, new EncodingOptions(), "libx264");

        Assert.DoesNotContain("setpts", args, StringComparison.Ordinal);
    }

    private static EncodingJobInfo BuildState(double? playbackSpeed)
    {
        var video = new MediaStream
        {
            Index = 0,
            Type = MediaStreamType.Video,
            Codec = "h264",
            Width = 1920,
            Height = 1080,
            AverageFrameRate = 23.976f,
            RealFrameRate = 23.976f,
        };
        var audio = new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = "ac3", Channels = 6 };

        return new EncodingJobInfo(TranscodingJobType.Hls)
        {
            MediaSource = new MediaSourceInfo
            {
                Container = "mkv",
                MediaStreams = new List<MediaStream> { video, audio },
            },
            VideoStream = video,
            AudioStream = audio,
            OutputVideoCodec = "libx264",
            OutputAudioCodec = "aac",
            OutputAudioChannels = 6,
            BaseRequest = new VideoRequestDto { PlaybackSpeed = playbackSpeed },
            IsVideoRequest = true,
            IsInputVideo = true,
        };
    }

    private static EncodingHelper CreateHelper()
        => new EncodingHelper(
            Mock.Of<IApplicationPaths>(),
            Mock.Of<IMediaEncoder>(),
            Mock.Of<ISubtitleEncoder>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<IConfigurationManager>(),
            Mock.Of<IPathManager>());
}
