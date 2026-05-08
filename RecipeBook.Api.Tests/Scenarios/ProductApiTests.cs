using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RecipeBook.Api.Contracts;
using RecipeBook.Api.Domain;
using RecipeBook.Api.Tests.Infrastructure;
using Xunit;

namespace RecipeBook.Api.Tests.Scenarios;

[Collection("Api collection")]
public class ProductsApiTests(TestWebAppFactory factory) : IAsyncLifetime
{
    private HttpClient _client = null!;
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _client = factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    public static TheoryData<double, double, double, HttpStatusCode> BjuBoundaryData => new()
    {
        { 0, 0, 0, HttpStatusCode.Created },
        { 100, 0, 0, HttpStatusCode.Created },
        { 0, 100, 0, HttpStatusCode.Created },
        { 0, 0, 100, HttpStatusCode.Created },
        { 50, 25, 25, HttpStatusCode.Created },
        { 33.33, 33.33, 33.34, HttpStatusCode.Created },
        { 99.99, 0.01, 0, HttpStatusCode.Created },
        { 100.01, 0, 0, HttpStatusCode.BadRequest },
        { 0, 100.01, 0, HttpStatusCode.BadRequest },
        { 0, 0, 100.01, HttpStatusCode.BadRequest },
        { 40, 40, 21, HttpStatusCode.BadRequest },
        { 0.1, 99.9, 0.1, HttpStatusCode.BadRequest }
    };

    public static TheoryData<string, HttpStatusCode> NameBoundaryData => new()
    {
        {"A", HttpStatusCode.BadRequest},
        {"AB", HttpStatusCode.Created},
        {"  AB  ", HttpStatusCode.Created},
        {"Пр", HttpStatusCode.Created},
        {" ", HttpStatusCode.BadRequest}
    };

    /// <summary>Эквивалентное разбиение + BVA для полей БЖУ.</summary>
    [Theory]
    [MemberData(nameof(BjuBoundaryData))]
    public async Task CreateProduct_ShouldValidateBjuBoundaries(double proteins, double fats, double carbs, HttpStatusCode expected)
    {
        var request = BuildProductRequest($"P-{Guid.NewGuid():N}", proteins, fats, carbs);
        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.StatusCode.Should().Be(expected);
    }

    /// <summary>Эквивалентное разбиение + BVA по длине названия.</summary>
    [Theory]
    [MemberData(nameof(NameBoundaryData))]
    public async Task CreateProduct_ShouldValidateNameLength(string name, HttpStatusCode expected)
    {
        var request = BuildProductRequest(name, 1, 1, 1);
        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(-0.01, 0, 0)]
    [InlineData(0, -0.01, 0)]
    [InlineData(0, 0, -0.01)]
    public async Task CreateProduct_ShouldRejectNegativeMacros(double proteins, double fats, double carbs)
    {
        var request = BuildProductRequest($"N-{Guid.NewGuid():N}", proteins, fats, carbs);
        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("name", "asc")]
    [InlineData("name", "desc")]
    [InlineData("calories", "asc")]
    [InlineData("calories", "desc")]
    [InlineData("proteins", "asc")]
    [InlineData("proteins", "desc")]
    [InlineData("fats", "asc")]
    [InlineData("fats", "desc")]
    [InlineData("carbs", "asc")]
    [InlineData("carbs", "desc")]
    public async Task ListProducts_ShouldSupportSortOptions(string sortBy, string direction)
    {
        await CreateProductAsync(BuildProductRequest($"Alpha-{Guid.NewGuid():N}", 5, 2, 1));
        await CreateProductAsync(BuildProductRequest($"Beta-{Guid.NewGuid():N}", 10, 3, 2));

        var response = await _client.GetAsync($"/api/products?sortBy={sortBy}&direction={direction}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("VEGAN", 1)]
    [InlineData("GLUTEN_FREE", 1)]
    [InlineData("SUGAR_FREE", 1)]
    [InlineData("VEGAN&flag=GLUTEN_FREE", 1)]
    [InlineData("VEGAN&flag=SUGAR_FREE", 1)]
    [InlineData("GLUTEN_FREE&flag=SUGAR_FREE", 1)]
    [InlineData("VEGAN&flag=GLUTEN_FREE&flag=SUGAR_FREE", 1)]
    public async Task ListProducts_ShouldFilterByFlags(string flagQuery, int expectedCount)
    {
        var uniq = Guid.NewGuid().ToString("N")[..6];
        await CreateProductAsync(BuildProductRequest($"Filtered-{uniq}", 1, 1, 1, [ExtraFlag.VEGAN, ExtraFlag.GLUTEN_FREE, ExtraFlag.SUGAR_FREE]));
        await CreateProductAsync(BuildProductRequest($"Other-{uniq}", 1, 1, 1));

        var response = await _client.GetAsync($"/api/products?query={uniq}&flag={flagQuery}");
        var body = await response.Content.ReadFromJsonAsync<List<Product>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Count.Should().Be(expectedCount);
    }

    [Fact]
    public async Task ListProducts_ShouldFilterByCombinedFlagsAndQuery()
    {
        var veganOnly = await CreateProductAsync(BuildProductRequest($"tofu-{Guid.NewGuid():N}", 10, 2, 4, flags: [ExtraFlag.VEGAN]));
        await CreateProductAsync(BuildProductRequest($"milk-{Guid.NewGuid():N}", 3, 2, 5));

        var response = await _client.GetAsync($"/api/products?query={veganOnly.Name[..4]}&flag=VEGAN");
        var body = await response.Content.ReadFromJsonAsync<List<Product>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Should().ContainSingle(x => x.Id == veganOnly.Id);
    }

    [Fact]
    public async Task DeleteProduct_WhenUsedInDish_ShouldReturnConflictWithDishNames()
    {
        var product = await CreateProductAsync(BuildProductRequest($"tomato-{Guid.NewGuid():N}", 1, 0.2, 4, flags: [ExtraFlag.VEGAN]));

        var dishRequest = new DishUpsertRequest("!салат tomato", null, new Nutrition(), [new DishIngredientRequest(product.Id, 100)], 150, null, [ExtraFlag.VEGAN]);
        var createDishResponse = await _client.PostAsJsonAsync("/api/dishes", dishRequest);
        createDishResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var deleteResponse = await _client.DeleteAsync($"/api/products/{product.Id}");
        var error = await deleteResponse.Content.ReadFromJsonAsync<ProductDeletionBlockedResponse>();

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        error!.DishNames.Should().ContainSingle();
    }

    [Fact]
    public async Task ProductCrud_HappyPath()
    {
        var created = await CreateProductAsync(BuildProductRequest($"CRUD-{Guid.NewGuid():N}", 7, 7, 7));

        var byId = await _client.GetAsync($"/api/products/{created.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = BuildProductRequest(created.Name + "-upd", 5, 5, 5);
        var updateResp = await _client.PutAsJsonAsync($"/api/products/{created.Id}", update);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var delResp = await _client.DeleteAsync($"/api/products/{created.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<Product> CreateProductAsync(ProductUpsertRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Product>(JsonOptions))!;
    }

    private static ProductUpsertRequest BuildProductRequest(string name, double proteins, double fats, double carbs, HashSet<ExtraFlag>? flags = null)
        => new(name, null, new Nutrition { Calories = 100, Proteins = proteins, Fats = fats, Carbs = carbs }, "test", ProductCategory.VEGETABLES, CookingRequirement.READY_TO_EAT, flags ?? []);
}
