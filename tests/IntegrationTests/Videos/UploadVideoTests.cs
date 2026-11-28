using System.Net;
using System.Net.Http.Json;
using Azure;
using Azure.Storage.Blobs;
using VideoProcessor.IntegrationTests.Harness;

namespace VideoProcessor.IntegrationTests.Videos;

[Collection(AppCollection.Name)]
public sealed class UploadVideoTests(AppFixture app)
{
    [Fact]
    public async Task Upload_slot_returns_a_write_only_url_scoped_to_one_blob()
    {
        var client = await app.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/videos", new { fileName = "holiday.mp4", size = 1024 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var slot = (await response.Content.ReadFromJsonAsync<UploadSlot>())!;
        slot.VideoId.Should().NotBeEmpty();

        // The URL can write its own blob...
        var blob = new BlobClient(slot.UploadUrl);
        await blob.UploadAsync(BinaryData.FromString("some bytes"));

        // ...but cannot read it back...
        var read = async () => await blob.DownloadContentAsync();
        (await read.Should().ThrowAsync<RequestFailedException>()).Which.Status.Should().Be(403);

        // ...and cannot write any other blob.
        var otherBlob = new BlobClient(new UriBuilder(slot.UploadUrl) { Path = blob.Uri.AbsolutePath + "-other" }.Uri);
        var writeOther = async () => await otherBlob.UploadAsync(BinaryData.FromString("other bytes"));
        (await writeOther.Should().ThrowAsync<RequestFailedException>()).Which.Status.Should().Be(403);
    }

    [Fact]
    public async Task Upload_slot_for_a_declared_size_over_the_maximum_is_refused()
    {
        var client = await app.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/videos", new { fileName = "huge.mp4", size = AppFixture.MaxUploadSizeBytes + 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors.Should().ContainKey("size");
    }

    [Fact]
    public async Task Confirming_an_mp4_with_audio_makes_the_video_ready_with_its_metadata()
    {
        var client = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlotAndUpload(client, "with-audio.mp4");

        var response = await client.PostAsync($"/videos/{slot.VideoId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var video = (await response.Content.ReadFromJsonAsync<ConfirmedVideo>())!;
        video.Id.Should().Be(slot.VideoId);
        video.Status.Should().Be("Ready");
        video.RejectionReason.Should().BeNull();
        video.Size.Should().Be(new FileInfo(SampleClip("with-audio.mp4")).Length);
        var metadata = video.Metadata!;
        metadata.Container.Should().Be("mp4");
        metadata.DurationSeconds.Should().BeApproximately(2.0, 0.1);
        metadata.Width.Should().Be(320);
        metadata.Height.Should().Be(240);
        metadata.VideoCodec.Should().Be("h264");
        metadata.AudioCodec.Should().Be("aac");
        metadata.Bitrate.Should().BePositive();
        metadata.FrameRate.Should().BeApproximately(25, 0.01);
        metadata.HasAudio.Should().BeTrue();
    }

    [Fact]
    public async Task Confirming_a_webm_makes_the_video_ready()
    {
        var client = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlotAndUpload(client, "clip.webm");

        var video = await Confirm(client, slot);

        video.Status.Should().Be("Ready");
        video.Metadata!.Container.Should().Be("webm");
        video.Metadata.VideoCodec.Should().Be("vp9");
        video.Metadata.AudioCodec.Should().Be("opus");
    }

    [Fact]
    public async Task Confirming_a_video_without_audio_makes_it_ready_without_an_audio_codec()
    {
        var client = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlotAndUpload(client, "no-audio.mp4");

        var video = await Confirm(client, slot);

        video.Status.Should().Be("Ready");
        video.Metadata!.VideoCodec.Should().Be("h264");
        video.Metadata.AudioCodec.Should().BeNull();
        video.Metadata.HasAudio.Should().BeFalse();
    }

    [Theory]
    // A text file with a video extension.
    [InlineData("not-a-video.mp4", "holiday.mp4", "NotAVideo")]
    // An FLV claiming to be an MP4: the container FFprobe detects decides, not the extension.
    [InlineData("disallowed-container.flv", "holiday.mp4", "ContainerNotAllowed")]
    // 3GP shares FFprobe's MP4 demuxer, but it isn't MP4.
    [InlineData("disallowed-container.3gp", "holiday.mp4", "ContainerNotAllowed")]
    public async Task Confirming_an_invalid_file_rejects_the_video_with_a_reason(
        string sampleClip, string fileName, string reason)
    {
        var client = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlotAndUpload(client, sampleClip, fileName);

        var video = await Confirm(client, slot);

        video.Status.Should().Be("Rejected");
        video.RejectionReason.Should().Be(reason);
        video.Metadata.Should().BeNull();
    }

    [Fact]
    public async Task Confirming_a_blob_larger_than_the_maximum_rejects_the_video_whatever_size_was_declared()
    {
        var client = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlot(client, "small.mp4", declaredSize: 1024);
        await new BlobClient(slot.UploadUrl).UploadAsync(new BinaryData(new byte[AppFixture.MaxUploadSizeBytes + 1]));

        var video = await Confirm(client, slot);

        video.Status.Should().Be("Rejected");
        video.RejectionReason.Should().Be("TooLarge");
        video.Size.Should().Be(AppFixture.MaxUploadSizeBytes + 1);
    }

    [Fact]
    public async Task Confirming_before_anything_was_uploaded_returns_400()
    {
        var client = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlot(client, "holiday.mp4", declaredSize: 1024);

        var response = await client.PostAsync($"/videos/{slot.VideoId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Confirming_a_video_that_is_no_longer_awaiting_upload_returns_409()
    {
        var client = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlotAndUpload(client, "with-audio.mp4");
        await Confirm(client, slot);

        var response = await client.PostAsync($"/videos/{slot.VideoId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Confirming_another_users_video_returns_404()
    {
        var owner = await app.CreateAuthenticatedClientAsync();
        var slot = await CreateSlotAndUpload(owner, "with-audio.mp4");
        var someoneElse = await app.CreateAuthenticatedClientAsync();

        var response = await someoneElse.PostAsync($"/videos/{slot.VideoId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Confirm(owner, slot)).Status.Should().Be("Ready", "the owner's Video must be untouched");
    }

    [Fact]
    public async Task Upload_slot_without_a_token_returns_401()
    {
        var response = await app.CreateClient().PostAsJsonAsync("/videos", new { fileName = "holiday.mp4", size = 1024 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<ConfirmedVideo> Confirm(HttpClient client, UploadSlot slot)
    {
        var response = await client.PostAsync($"/videos/{slot.VideoId}/confirm", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ConfirmedVideo>())!;
    }

    private static async Task<UploadSlot> CreateSlot(HttpClient client, string fileName, long declaredSize)
    {
        var response = await client.PostAsJsonAsync("/videos", new { fileName, size = declaredSize });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UploadSlot>())!;
    }

    private static string SampleClip(string name) => Path.Combine(AppContext.BaseDirectory, "SampleClips", name);

    /// <summary>Creates an Upload slot and uploads the sample clip to it, as a client would, without confirming.</summary>
    private static async Task<UploadSlot> CreateSlotAndUpload(HttpClient client, string sampleClip, string? fileName = null)
    {
        var path = SampleClip(sampleClip);
        var slot = await CreateSlot(client, fileName ?? sampleClip, new FileInfo(path).Length);

        await new BlobClient(slot.UploadUrl).UploadAsync(path);
        return slot;
    }

    private sealed record ConfirmedVideo(
        Guid Id,
        string FileName,
        string Status,
        string? RejectionReason,
        long? Size,
        ConfirmedMetadata? Metadata);

    private sealed record ConfirmedMetadata(
        string Container,
        double? DurationSeconds,
        int? Width,
        int? Height,
        string? VideoCodec,
        string? AudioCodec,
        long? Bitrate,
        double? FrameRate,
        bool HasAudio);

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);

    private sealed record UploadSlot(Guid VideoId, Uri UploadUrl, DateTimeOffset ExpiresAt);
}
