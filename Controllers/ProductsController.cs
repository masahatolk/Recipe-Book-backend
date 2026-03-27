using Microsoft.AspNetCore.Mvc;
using RecipeBook.Api.Contracts;
using RecipeBook.Api.Domain;
using RecipeBook.Api.Services;

namespace RecipeBook.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(RecipeService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Product>>> List(
        [FromQuery] string? category,
        [FromQuery] string? cookingRequirement,
        [FromQuery(Name = "flag")] List<ExtraFlag>? flags,
        [FromQuery] string? query,
        [FromQuery] string sortBy = "name",
        [FromQuery] string direction = "asc")
    {
        var result = await service.ListProductsAsync(category, cookingRequirement, flags ?? [], query, sortBy, direction);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> Get(Guid id)
    {
        var product = await service.GetProductAsync(id);
        return product is null ? NotFound(new ApiErrorResponse("Product not found")) : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] ProductUpsertRequest request)
    {
        var created = await service.CreateProductAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Product>> Update(Guid id, [FromBody] ProductUpsertRequest request)
    {
        var updated = await service.UpdateProductAsync(id, request);
        return updated is null ? NotFound(new ApiErrorResponse("Product not found")) : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteProductAsync(id);
        return NoContent();
    }
}