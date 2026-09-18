using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TF.Extensions;
using TF.Models;
using TF.ViewModels;
using Trust_Finance.Services;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoryController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] CategoryService service)
        => Ok(new ResultViewModel<List<Category>>(
            await service.GetAllAsync(User.GetUserId())));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        [FromServices] CategoryService service)
    {
        var category = await service.GetByIdAsync(id, User.GetUserId());

        if (category == null)
            return NotFound(new ResultViewModel<Category>("Category not found"));

        return Ok(new ResultViewModel<Category>(category));
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] EditorCategoryViewModel model,
        [FromServices] CategoryService service)
    {
        try
        {
            var category = await service.CreateAsync(
                model.Name, model.Slug, User.GetUserId());
            return Created(
                $"api/categories/{category.Id}",
                new ResultViewModel<Category>(category));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ResultViewModel<Category>(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(
        int id,
        [FromBody] EditorCategoryViewModel model,
        [FromServices] CategoryService service)
    {
        try
        {
            var category = await service.UpdateAsync(
                id, model.Name, model.Slug, User.GetUserId());
            return Ok(new ResultViewModel<Category>(category));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ResultViewModel<Category>("Category not found"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ResultViewModel<Category>(ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromServices] CategoryService service)
    {
        try
        {
            var category = await service.DeleteAsync(id, User.GetUserId());
            return Ok(new ResultViewModel<Category>(category));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ResultViewModel<Category>("Category not found"));
        }
    }
}
