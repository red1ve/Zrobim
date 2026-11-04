using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zrobim.Api.Auth;
using Zrobim.Api.Data;

namespace Zrobim.Api.Catalog;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/categories")]
public class CategoryAdminController(ZrobimDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<AdminCategoryResponse>> GetCategories(CancellationToken ct) =>
        db.Categories.OrderByDescending(c => c.IsActive).ThenBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new AdminCategoryResponse(c.Id, c.Name, c.IsActive,
                db.Services.Count(s => s.CategoryId == c.Id)))
            .ToListAsync(ct);

    [HttpPost]
    public Task<IActionResult> Create(SaveCategoryRequest request, CancellationToken ct) =>
        Save(null, request, ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Edit(Guid id, SaveCategoryRequest request, CancellationToken ct) =>
        Save(id, request, ct);

    private async Task<IActionResult> Save(Guid? id, SaveCategoryRequest request, CancellationToken ct)
    {
        var category = id is null ? new Category() : await db.Categories.FindAsync([id.Value], ct);
        if (category is null)
            return NotFound();

        category.Name = request.Name.Trim();
        category.IsActive = request.IsActive!.Value;
        if (id is null)
            db.Categories.Add(category);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "categories_name_key"
            })
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Категория с таким названием уже существует.");
        }

        return id is null
            ? StatusCode(StatusCodes.Status201Created, new AdminCategoryResponse(category.Id, category.Name, category.IsActive, 0))
            : NoContent();
    }
}

public sealed record SaveCategoryRequest(
    [Required(ErrorMessage = "Укажите название категории.")]
    [MaxLength(100, ErrorMessage = "Название должно быть не длиннее 100 символов.")] string Name,
    [Required] bool? IsActive);

public sealed record AdminCategoryResponse(Guid Id, string Name, bool IsActive, int ServiceCount);
