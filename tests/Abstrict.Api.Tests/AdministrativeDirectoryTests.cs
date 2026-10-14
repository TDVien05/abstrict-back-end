using System.Net;
using System.Text;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Tests;

public sealed class AdministrativeDirectoryTests
{
    private const string ProvincesJson = """
        {"provinces":[{"code":"79","name":"Thành phố Hồ Chí Minh"},{"code":"01","name":"Thành phố Hà Nội"},{"code":"04","name":"Tỉnh Cao Bằng"}]}
        """;

    private const string CommunesJson = """
        {"communes":[
          {"code":"26737","name":"Phường Bến Nghé","districtCode":"760","districtName":"Quận 1","provinceCode":"79"},
          {"code":"26734","name":"Phường Tân Định","districtCode":"760","districtName":"Quận 1","provinceCode":"79"},
          {"code":"26788","name":"Xã Củ Chi","districtCode":"783","districtName":"Huyện Củ Chi","provinceCode":"79"},
          {"code":"00001","name":"Phường Phúc Xá","districtCode":"001","districtName":"Quận Ba Đình","provinceCode":"01"}
        ]}
        """;

    [Fact]
    public async Task GetAsync_BuildsProvincesDistrictsAndWardsFromAddressKit()
    {
        var directory = Create(new StubHandler(Serve(ProvincesJson, CommunesJson)));

        var snapshot = await directory.GetAsync(CancellationToken.None);

        Assert.False(snapshot.IsFallback);
        Assert.Equal(["Hà Nội", "Cao Bằng", "Hồ Chí Minh"], snapshot.Provinces.Select(p => p.Name));
        var hcm = snapshot.Provinces.Single(p => p.Code == 79);
        Assert.Equal(["Quận 1", "Huyện Củ Chi"], hcm.Districts.Select(d => d.Name));
        Assert.Equal(["Phường Tân Định", "Phường Bến Nghé"], snapshot.FindDistrict(79, 760)!.Wards.Select(w => w.Name));
        Assert.Empty(snapshot.Provinces.Single(p => p.Code == 4).Districts);
        Assert.Null(snapshot.FindDistrict(79, 999));
        Assert.Null(snapshot.FindDistrict(1, 760));
        Assert.Equal(["Phường Phúc Xá"], snapshot.FindDistrict(1, 1)!.Wards.Select(w => w.Name));
    }

    [Fact]
    public async Task GetAsync_CachesTheSnapshotBetweenCalls()
    {
        var handler = new StubHandler(Serve(ProvincesJson, CommunesJson));
        var directory = Create(handler);

        var first = await directory.GetAsync(CancellationToken.None);
        var second = await directory.GetAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetAsync_FallsBackToEmbeddedDistrictsWhenAddressKitFails()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var directory = Create(handler);

        var snapshot = await directory.GetAsync(CancellationToken.None);
        var again = await directory.GetAsync(CancellationToken.None);

        Assert.True(snapshot.IsFallback);
        Assert.Same(snapshot, again);
        Assert.Equal(63, snapshot.Provinces.Count);
        var hcm = snapshot.Provinces.Single(p => p.Name == "Hồ Chí Minh");
        Assert.Equal(22, hcm.Districts.Count);
        Assert.Empty(hcm.Districts[0].Wards);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetAsync_RejectsEmptyAddressKitPayloadAndFallsBack()
    {
        var directory = Create(new StubHandler(Serve("""{"provinces":[]}""", """{"communes":[]}""")));

        Assert.True((await directory.GetAsync(CancellationToken.None)).IsFallback);
    }

    private static AdministrativeDirectory Create(HttpMessageHandler handler) => new(
        new StubFactory(handler),
        Microsoft.Extensions.Options.Options.Create(new AddressKitOptions()),
        TimeProvider.System,
        NullLogger<AdministrativeDirectory>.Instance);

    private static Func<HttpRequestMessage, HttpResponseMessage> Serve(string provinces, string communes) => request =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/provinces") ? provinces : communes, Encoding.UTF8, "application/json")
        };

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://addresskit.test/") };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(respond(request));
        }
    }
}
