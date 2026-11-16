using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Features.Notes;

namespace SlideWorks.WebApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
}
