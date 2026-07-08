using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TF.Extensions;
using TF.ViewModels;
using TF.Models;
using Trust_Finance.Services;

namespace Trust_Finance.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] TransactionService service)
    {
        var userId = User.GetUserId();
        var transactions = await service.GetAllAsync(userId);
        return Ok(new ResultViewModel<List<Transaction>>(transactions));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        [FromServices] TransactionService service)
    {
        var userId = User.GetUserId();
        var transaction = await service.GetByIdAsync(id, userId);

        if (transaction == null)
            return NotFound(new ResultViewModel<Transaction>("Transaction not found"));

        return Ok(new ResultViewModel<Transaction>(transaction));
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] EditorTransactionViewModel model,
        [FromServices] TransactionService service)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ResultViewModel<Transaction>(ModelState.GetErrors()));

        var userId = User.GetUserId();

        var transaction = await service.CreateAsync(
            model.Description,
            model.Amount,
            model.Date,
            model.CategoryId,
            userId);

        return Created(
            $"api/transactions/{transaction.Id}",
            new ResultViewModel<Transaction>(transaction));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(
        int id,
        [FromBody] EditorTransactionViewModel model,
        [FromServices] TransactionService service)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ResultViewModel<Transaction>(ModelState.GetErrors()));

        try
        {
            var userId = User.GetUserId();

            var transaction = await service.UpdateAsync(
                id,
                model.Description,
                model.Amount,
                model.Date,
                model.CategoryId,
                userId);

            return Ok(new ResultViewModel<Transaction>(transaction));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ResultViewModel<Transaction>("Transaction not found"));
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromServices] TransactionService service)
    {
        try
        {
            var userId = User.GetUserId();
            var transaction = await service.DeleteAsync(id, userId);
            return Ok(new ResultViewModel<Transaction>(transaction));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ResultViewModel<Transaction>("Transaction not found"));
        }
    }
}
