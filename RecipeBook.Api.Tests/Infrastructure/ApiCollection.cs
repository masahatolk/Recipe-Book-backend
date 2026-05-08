using Xunit;

namespace RecipeBook.Api.Tests.Infrastructure;

[CollectionDefinition("Api collection")]
public class ApiCollection : ICollectionFixture<TestWebAppFactory>
{
}